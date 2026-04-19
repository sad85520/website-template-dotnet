# Template Audit — 2026-04-19

**Scope**: `website-template-dotnet`（全 repo，含 backend / frontend / infra / CI）
**Reviewers**: general-purpose（C#/ASP.NET Core） / typescript-reviewer / security-reviewer
**Basis**: 2026-04-18 audit 之後的 hardening（JWT / DB fail-fast、Serilog、nginx CSP 等）完成後的全新 independent scan；舊 finding 大致都已閉，本檔只列 NEW 發現
**Sibling repo**: `website-template-python`（2026-04-19 audit 獨立檔）

---

## P0 Critical

### #1 Named rate limiter policies 無 partition key → 全站共用同一 bucket，觸即 DoS
- 檔案：[src/backend/src/WebTemplate.Api/Infrastructure/Extensions/ServiceCollectionExtensions.cs:146-162](../../src/backend/src/WebTemplate.Api/Infrastructure/Extensions/ServiceCollectionExtensions.cs)
- 問題：`AddFixedWindowLimiter("auth", ...)` / `("api", ...)` 以 policy name 直接註冊，未用 `AddPolicy` + `PartitionedRateLimiter.Create(ctx => RateLimitPartition.GetFixedWindowLimiter(partitionKey: ip, ...))`。結果：所有客戶端共用同一 bucket，`auth` policy 全服務每分鐘只能 10 次登入，任一攻擊者每分鐘打 10 次就能讓全站使用者無法登入
- 修法：
  ```csharp
  options.AddPolicy("auth", httpContext =>
      RateLimitPartition.GetFixedWindowLimiter(
          httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
          _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
  ```
  + 搭配 `UseForwardedHeaders` 解析反向代理後的真實 IP（否則全打到 upstream IP）

### #2 `MigrateAsync()` 首次啟動失敗 — 無 `Migrations/` 目錄
- 檔案：[src/backend/src/WebTemplate.Api/Program.cs:62-67](../../src/backend/src/WebTemplate.Api/Program.cs)
- 問題：repo 無 `Migrations/` 資料夾。`db.Database.MigrateAsync()` 只建立空的 `__EFMigrationsHistory` table，**不會**建立 `Users`/`RefreshTokens`；dev 啟動後第一個註冊/登入請求一律爆 `Invalid object name 'Users'`。downstream clone 完全跑不起來
- 修法：`dotnet ef migrations add InitialCreate -p src/backend/src/WebTemplate.Api` 產初始 migration 並 commit

---

## P1 High

### Backend

#### #3 Production Serilog 未設 JSON sink — 宣稱的 prod JSON 實際上仍是 console text
- 檔案：[src/backend/src/WebTemplate.Api/appsettings.Production.json](../../src/backend/src/WebTemplate.Api/appsettings.Production.json)、[WebTemplate.Api.csproj:24-25](../../src/backend/src/WebTemplate.Api/WebTemplate.Api.csproj)
- 問題：csproj 只引 `Serilog.Sinks.Console`；`appsettings.Production.json` 只覆寫 `MinimumLevel`，無 `WriteTo` 區段，也無 `Serilog.Formatting.Compact` 套件。`UseSerilog` 的 fallback 在 prod 仍輸出人類文字
- 修法：加 `Serilog.Formatting.Compact` 套件；`appsettings.Production.json` 加 `"Serilog.WriteTo": [{ "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" } }]`

#### #4 `appsettings.json` 預設 `AllowedHosts: "localhost;127.0.0.1"` 讓 prod 部署全 404
- 檔案：[src/backend/src/WebTemplate.Api/appsettings.json:11](../../src/backend/src/WebTemplate.Api/appsettings.json)
- 問題：Host filtering middleware 會拒絕不在清單內的 Host header。Template 預設只允許 localhost，downstream 用真實網域部署時 Kestrel 直接 400 並寫 warning "The allowed hosts does not contain..."，極難 debug
- 修法：`appsettings.json` 改 `"AllowedHosts": "*"`；`appsettings.Production.json` 加 `"AllowedHosts": "__REPLACE_WITH_YOUR_DOMAIN__"` 並於 README 提醒覆寫

#### #5 `GlobalExceptionHandler` 未檢查 `Response.HasStarted`，串流情境拋第二次例外
- 檔案：[src/backend/src/WebTemplate.Api/Infrastructure/Middleware/GlobalExceptionHandler.cs:31-34](../../src/backend/src/WebTemplate.Api/Infrastructure/Middleware/GlobalExceptionHandler.cs)
- 問題：若例外發生時 response 已開始寫出（大型 `IAsyncEnumerable` 中斷），`httpContext.Response.StatusCode = ...` 會拋 `InvalidOperationException`，新例外覆蓋原始例外
- 修法：開頭加 `if (httpContext.Response.HasStarted) { logger.LogWarning(...); return false; }`

#### #6 Model validation 失敗回 `ValidationProblemDetails`，與成功路徑 `ApiResponse<T>` 信封不一致
- 檔案：[src/backend/src/WebTemplate.Api/Program.cs:26](../../src/backend/src/WebTemplate.Api/Program.cs)、`Common/Models/ApiResponse.cs`
- 問題：`[ApiController]` 預設 RFC 7807（`title`/`errors` 欄位），但成功路徑與 `GlobalExceptionHandler` 都用 `ApiResponse<T>`（`success`/`data`/`message`/`errors` 欄位）。前端要寫兩套解析器；`FieldError` class 根本無處填入
- 修法：
  ```csharp
  builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx =>
  {
      var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
          .SelectMany(kv => kv.Value!.Errors.Select(e => new FieldError { Field = kv.Key, Message = e.ErrorMessage }));
      return new BadRequestObjectResult(ApiResponse<object>.Fail("Validation failed.", errors));
  });
  ```

#### #7 `RegisterAsync` TOCTOU — 並發相同 email 拋 500 而非 409
- 檔案：[src/backend/src/WebTemplate.Api/Modules/Accounts/Services/AuthService.cs:51-67](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Services/AuthService.cs)
- 問題：`ExistsByEmailAsync` → `CreateAsync` 非原子，DB unique index 在並發第二筆 `SaveChangesAsync` 拋 `DbUpdateException` (SqlException 2601/2627)。不是 `AppException`，`GlobalExceptionHandler` 轉為 500，client 無法判斷衝突 vs 系統故障
- 修法：`CreateAsync` 外包 `try/catch (DbUpdateException ex) when (IsUniqueViolation(ex)) throw new AppConflictException("Email is already registered.")`；或 `GlobalExceptionHandler` 加 arm `DbUpdateException when IsUniqueViolation(ex) => (409, "Conflict.")`

### Frontend

#### #8 `client.ts:64` 對 `error.config` 直接寫 `_retry`（config 可能 undefined）
- 檔案：[src/frontend/src/api/client.ts:64](../../src/frontend/src/api/client.ts)
- 問題：`originalRequest._retry = true` — network error 時 `error.config` 為 undefined，runtime 拋錯
- 修法：`if (!originalRequest) return Promise.reject(error)` 先守

#### #9 `fetchCurrentUser` 拋錯未被 `login`/`tryRefreshToken` 捕捉
- 檔案：[src/frontend/src/stores/auth.store.ts:59-63](../../src/frontend/src/stores/auth.store.ts)
- 問題：`fetchCurrentUser` 無 try/catch。`login` 僅有 `finally` 無 `catch`，rejection bubble 到 useAuth 同樣無處理 → 靜默崩潰
- 修法：`fetchCurrentUser` 內部吞錯，或各 caller try/catch

#### #10 `auth.store.ts` `login()` 無 catch，API error 直接 unhandled rejection
- 檔案：[src/frontend/src/stores/auth.store.ts:24-36](../../src/frontend/src/stores/auth.store.ts)
- 問題：與 python sibling 對比，python 用 `try/catch` + `extractProblem` 回 `AuthResult`（永不 throw），dotnet 只有 `finally` → 直接 unhandled
- 修法：照 python pattern 改為 catch AxiosError 回 `ApiResponse` shape 失敗

#### #11 `useAuth.login` 假設 `result.success` / `result.message` 存在
- 檔案：[src/frontend/src/composables/useAuth.ts:22-33](../../src/frontend/src/composables/useAuth.ts)
- 問題：若 store.login 拋錯 result 未 assign，後續 `if (result.success)` unreachable 且 composable 也無 catch
- 修法：wrap `authStore.login` 在 try/catch，fallback `notificationStore.error('登入失敗')`

#### #12 `vite.config.ts` 無 `allowedHosts`
- 檔案：[src/frontend/vite.config.ts:15](../../src/frontend/vite.config.ts)
- 問題：`host: '0.0.0.0'` 未搭配 `server.allowedHosts`，DNS rebinding 保護未啟
- 修法：加 `allowedHosts: ['localhost', '127.0.0.1']` 或註明 Docker-only 意圖

### Security / Infra

#### #13 GitHub Actions 第三方 action 全部 semver tag，未 pin SHA
- 檔案：`.github/workflows/{ci,cd,cd-production}.yml`
- 問題：`actions/checkout@v4` 等若 tag 被 force-push 覆蓋，CI 執行被竄改的 action 可讀 `secrets.*`
- 修法：`uses: actions/checkout@<full-sha> # v4.2.2` 格式固定 SHA；`pinact` / Dependabot 自動維護

#### #14 k8s backend/frontend pod `readOnlyRootFilesystem: false`
- 檔案：[infra/k8s/backend/deployment.yml:26](../../infra/k8s/backend/deployment.yml)、[infra/k8s/frontend/deployment.yml:26](../../infra/k8s/frontend/deployment.yml)
- 修法：設 `true`，`/tmp` 等可寫路徑改 emptyDir

#### #15 k8s pod 全部無 `seccompProfile`
- 檔案：`infra/k8s/{backend,frontend,database}/deployment.yml`
- 修法：`seccompProfile: { type: RuntimeDefault }`

#### #16 k8s MSSQL pod 無 securityContext
- 檔案：[infra/k8s/database/deployment.yml](../../infra/k8s/database/deployment.yml)
- 問題：MSSQL Linux image 目前仍需 root，但可設 `allowPrivilegeEscalation: false` + `capabilities.drop: [ALL]` 降權
- 修法：加上述欄位；comment 明示推薦使用 managed service

#### #17 k8s Ingress 無 TLS / 無 HTTPS redirect
- 檔案：[infra/k8s/ingress.yml](../../infra/k8s/ingress.yml)
- 修法：加 `tls:` + `nginx.ingress.kubernetes.io/ssl-redirect: "true"`

#### #18 `UsersController` 無 `[EnableRateLimiting]` 標注，admin 可被 enumerated
- 檔案：[src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/UsersController.cs](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/UsersController.cs)
- 問題：雖需 Admin role，token 洩漏後可無速率限制列舉使用者
- 修法：class-level 加 `[EnableRateLimiting("api")]`

#### #19 Refresh token cookie `Secure = true` 硬碼，dev HTTP 無法測試 refresh
- 檔案：[src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/AuthController.cs:30](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/AuthController.cs)
- 問題：`BuildRefreshCookieOptions()` 永遠 `Secure=true`，HTTP dev compose 下瀏覽器拒送 cookie → refresh 端點永遠 401
- 修法：注入 `IWebHostEnvironment` 以 `env.IsProduction()` 決定 `Secure`；或 `JwtSettings.RefreshTokenCookieSecure` 由環境覆蓋

---

## P2 Medium

### Backend

- **#20 `[EnableRateLimiting("auth")]` 套 class-level，refresh/logout 吃 login 配額** — [AuthController.cs:15](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/AuthController.cs)；移到 `Register`/`Login` 兩 action；refresh 改 `"api"` 或另開 `"refresh"` policy
- **#21 `CreateRefreshTokenAsync` detach + 改寫 `TokenHash` 回傳明文** — [TokenService.cs:71-79](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Services/TokenService.cs)：欄位名一時 hash 一時明文，re-attach 風險；改介面回 `(RefreshToken, string RawToken)` 或 record
- **#22 `UserService.GetAllAsync` 回 `ApiResponse<>` → 服務層洩漏 HTTP envelope** — [UserService.cs:20-33](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Services/UserService.cs)；改回 `PagedResult<UserDto>` 純資料，controller 自包
- **#23 `IUserRepository.SaveChangesAsync` 洩漏 EF UoW 到 service** — [IUserRepository.cs:43](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Repositories/Interfaces/IUserRepository.cs)；加 `UpdateAsync(User)` / `IncrementFailedLoginsAsync` 語意方法，repo 內部 save
- **#24 無全域 `FallbackPolicy` — 忘加 `[Authorize]` 即匿名**；`AddAuthorizationBuilder().SetFallbackPolicy(...RequireAuthenticatedUser())`，公開端點明示 `[AllowAnonymous]`
- **#25 `JwtSecurityTokenHandler` 為 legacy，claim mapping 未清除** — [TokenService.cs:1,41](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Services/TokenService.cs)；改 `JsonWebTokenHandler` + `JwtSecurityTokenHandler.DefaultMapInboundClaims = false`；claims 用 `JwtRegisteredClaimNames.Sub` 標準名
- **#26 `ApiResponse<T>` 為 mutable class + public setter（違反 rules immutability 偏好）** — [Common/Models/ApiResponse.cs:5-53](../../src/backend/src/WebTemplate.Api/Common/Models/ApiResponse.cs)；改 `sealed record` + init-only + 靜態工廠
- **#27 `ApiResponse<object>.Ok(null!)` 以 null-forgiving 隱藏契約違反** — [AuthController.cs:99](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/AuthController.cs)；新增 `Ok()` 無參數 overload

### Frontend

- **#28 `NotificationContainer.vue:11` dismiss button 無 `aria-label`**
- **#29 `BaseInput.vue` `required` prop 未宣告**
- **#30 `BaseInput.vue` 錯誤訊息無 `aria-describedby`**
- **#31 `router/index.ts` `RouteMeta` 未 augmentation**
- **#32 `ui.store.ts:17` `document` 存取 SSR 不安全**
- **#33 `notification.store.ts:23` `setTimeout` ID 未保存**
- **#34 `tests/unit/stores/auth.test.ts:41-46` 成功路徑未斷言 `result.success`** — 下游若改 return shape 不會被抓到
- **#35 login failure path（store 拋錯）無測試** — 對應 #10，失敗模式缺 coverage

### Security / Infra

- **#36 `appsettings.Development.json` 被 gitignore 排除但 working tree 存在**；提供 `appsettings.Development.json.example` 並於 `dev-setup.md` 說明 `cp` 步驟
- **#37 nginx HSTS header 設在 `listen 80`（RFC 6797 §8.1 瀏覽器忽略）** — [infra/nginx/nginx.conf:38](../../infra/nginx/nginx.conf)
- **#38 `.env` / `.env.example` 使用固定示範密碼（`YourStrong!Passw0rd`）** — 改 `<REPLACE_ME>` 並附生成指令
- **#39 Refresh cookie `Domain` 未明示設定** — [AuthController.cs:28-34](../../src/backend/src/WebTemplate.Api/Modules/Accounts/Controllers/AuthController.cs)；加 comment 或從 config 讀

---

## P3 Low

- **#40 Dev placeholder `__REPLACE_WITH_LOCAL_*__` 非空字串，通過 fail-fast** — [appsettings.Development.json:3,6](../../src/backend/src/WebTemplate.Api/appsettings.Development.json)；fail-fast 檢查加 `StartsWith("__REPLACE_WITH_")` sentinel
- **#41 `User.Email` 唯一索引未 CaseInsensitive collation，也未 normalize** — [AppDbContext.cs:27](../../src/backend/src/WebTemplate.Api/Infrastructure/Data/AppDbContext.cs)；`NormalizedEmail` 欄位或 register/login 前 `ToLowerInvariant()`
- **#42 Integration test 用 `UseInMemoryDatabase`，不驗證 SQL 約束 / concurrency** — [CustomWebApplicationFactory.cs:64-65](../../src/backend/tests/WebTemplate.Api.Tests/Integration/CustomWebApplicationFactory.cs)；改 `Testcontainers.MsSql` 或 SQLite in-memory
- **#43 `HealthController` 路由 `api/health` 與 Dockerfile 註解的 `/health` 不一致** — [HealthController.cs:8](../../src/backend/src/WebTemplate.Api/Controllers/HealthController.cs)、[Dockerfile:32-33](../../src/backend/src/WebTemplate.Api/Dockerfile)；改 `[Route("health")]` 或同步註解
- **#44 Controllers 未使用 `[ProducesResponseType]`，OpenAPI 只有 200 schema** — `AuthController` / `UsersController` 全 actions
- **#45 `docker-compose` 未強制從 env 注入 secret 覆寫 `appsettings.Development.json` placeholder**；`environment: [Jwt__Secret, ConnectionStrings__DefaultConnection]` + `.env.example` 附 `openssl rand -base64 48` 指令
- **#46 k8s MSSQL pod 無 `fsGroup`** — PVC 掛載 `/var/opt/mssql`；確認 GID 後設定，或 comment 標示為 managed service 替換點
- **#47 `gitleaks-action@v2` tag 未固定**（同 #13 子問題）
- **#48 CI 無 `__REPLACE_WITH_` placeholder 掃描**；加一步 grep 或依賴 gitleaks

---

## Cross-repo shared issues（與 website-template-python 共通）

- **C1** GitHub Actions SHA pin（#13）
- **C2** k8s `readOnlyRootFilesystem` + seccomp（#14/#15）
- **C3** Ingress 無 TLS（#17）
- **C4** nginx HSTS on :80（#37）
- **C5** `.env.example` 明文示範密碼（#38）
- **C6** frontend `client.ts` `_retry` / `fetchCurrentUser` 錯誤處理（#8/#9）
- **C7** `vite.config.ts` 無 `allowedHosts`（#12）
- **C8** `BaseInput`/`NotificationContainer` a11y / `RouteMeta` / `ui.store` SSR（#28–#33）

## Symmetry violations（dotnet 落後於 python 的對稱點）

- `auth.store.ts` 錯誤處理：python 用 `try/catch + extractProblem` 回 `AuthResult`（永不 throw），dotnet 只有 `finally` → **功能性 bug**（對應 #10）
- `auth.test.ts` mock 型別：python 用 `satisfies AxiosResponse<LoginResponse>`，dotnet 全 `as any`（對應 #34/#35）
- `authApi` generic shape：python 用 bare `LoginResponse`，dotnet 用 `ApiResponse<LoginResponse>`（正確的後端差異，但 README 應明示兩 template 的 envelope 契約）

---

## 統計

| 嚴重度 | 件數 |
|--------|------|
| **P0** | **2** |
| P1 | 17（backend 5 / frontend 5 / security 7） |
| P2 | 20（backend 8 / frontend 8 / security 4） |
| P3 | 9 |

**建議優先**：#1（rate limiter partition）→ #2（Migrations 缺失）→ #3（Serilog JSON）→ #4（AllowedHosts 地雷）→ #7（TOCTOU 500→409）→ #9/#10（frontend 未捕獲錯誤）→ #13（supply chain）
