# Template Audit — 2026-04-18

**Scope**: `website-template-dotnet` (全 repo)
**Reviewers**: security-reviewer / typescript-reviewer / architect
**Sibling repo**: `website-template-python`（對稱性審核已完成，評分 4.2/5）

## P1 High

### #3 所有 Dockerfile 未設 non-root USER（both repos）
- 檔案：`src/backend/src/WebTemplate.Api/Dockerfile`、`src/frontend/Dockerfile`
- 修法：runtime stage 加 `RUN addgroup --system app && adduser --system --ingroup app app` + `USER app`

### #4 Base image 用浮動 tag（both）
- 檔案：所有 `Dockerfile` — `node:22-alpine`、`nginx:alpine`、.NET runtime
- 修法：pin 到 minor 或 digest

### #5 nginx.spa.conf 缺安全 headers（both）
- 檔案：`src/frontend/nginx.spa.conf`
- 缺：CSP、HSTS、X-Frame-Options、X-Content-Type-Options、Referrer-Policy
- 修法：將 `infra/nginx/nginx.conf` L23–28 的 header block 複製進來

### #6 `infra/nginx/nginx.conf` CSP 含 `unsafe-inline`（both）
- 檔案：`infra/nginx/nginx.conf:27`
- 問題：`script-src 'self' 'unsafe-inline'` / `style-src 'self' 'unsafe-inline'` 使 CSP 失效
- 修法：移除 `'unsafe-inline'`，改用 nonce/hash

### #7 CI 無 lint / type-check / coverage gate（dotnet 獨有）
- 檔案：`.github/workflows/ci.yml`
- 對照 python `ci.yml`：有 Ruff / Mypy / Codecov upload
- 修法：加 `dotnet format --verify-no-changes` + coverage artifact upload，順序對齊 `lint → build → test-with-coverage → upload-coverage`

### #8 Makefile 缺 `lint` target（dotnet 獨有）
- 檔案：`Makefile`
- 修法：加 `lint: docker compose run --rm backend dotnet format --verify-no-changes`

### #13 ESLint 未啟用 type-checked rules（both frontend）
- 檔案：`src/frontend/eslint.config.js`
- 問題：缺 `no-floating-promises`、`no-misused-promises`、`await-thenable`
- 修法：`parserOptions` 加 `project: true`，改用 `tsPlugin.configs['flat/recommended-type-checked']`

### #14 `@typescript-eslint/no-explicit-any` 設為 `warn`（both frontend）
- 檔案：`src/frontend/eslint.config.js:26`
- 修法：改為 `error`，或在 `tests/` 目錄 override

### #16 `AllowedHosts: "*"` 在 base appsettings（dotnet）
- 檔案：`src/backend/src/WebTemplate.Api/appsettings.json:11`
- 修法：加 `appsettings.Production.json` override 或 env-var

### Extra: Token 缺失時靜默降級（dotnet frontend）
- 檔案：`src/frontend/src/api/client.ts:77`
- 問題：`response.data.data?.accessToken ?? ''` 把缺失 token 變空字串繼續重試
- 修法：`!newToken` 時 throw，讓 catch 清 auth state

### Extra: auth.store.ts 公開方法缺 return type（dotnet frontend）
- 檔案：`src/frontend/src/stores/auth.store.ts`
- 對照：python 版本已全標注 `Promise<AuthResult>` / `Promise<void>`
- 修法：補齊

## P2 Medium

### GitHub Actions `contents: write` 過寬
- 檔案：`.github/workflows/cd.yml:73`、`cd-production.yml:26`
- 修法：加 branch protection + 拆 git-push 到獨立 job

### `tsconfig.node.json` include 含不存在檔案（both frontend）
- 檔案：`src/frontend/tsconfig.node.json:17`
- 內容：`tailwind.config.ts`、`postcss.config.js` 不存在（Tailwind v4）
- 修法：移除

### `vitest.config.ts` coverage 無 threshold（both frontend）
- 修法：加 `coverage.thresholds.lines: 80`

### `.env.example` 詳細度 vs python 差異過大
- 對照：python 列 12 個變數，dotnet 只有 3 個
- 修法：補上 `Jwt__Issuer`、`Jwt__Audience`、`ConnectionStrings__*` override 作文件

### `appsettings.Development.json` 占位密碼無 fail-fast
- 檔案：`src/backend/src/WebTemplate.Api/appsettings.Development.json:3-6`
- 修法：`ServiceCollectionExtensions.cs:45-46` 的 JWT 驗證 pattern 套用到 connection string

## P3 Low

### HSTS 送在 HTTP-only 監聽器（both）
- 檔案：`infra/nginx/nginx.conf:28`
- 修法：移到 `:443` block，或加註解說明 TLS 由上游終止

### `package.json` lint script 用 deprecated `--ext` flag（both frontend）
- 修法：flat config 不需要 `--ext`，移除

### `Dockerfile.dev` 無 source 複製或 VOLUME 提示（both frontend）
- 修法：加註解說明需搭配 compose volume mount
