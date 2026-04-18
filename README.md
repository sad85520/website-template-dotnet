# website-template-dotnet

現代前後端分離架構網站起手式 Template（.NET 版）。

## 技術棧

| 層級 | 技術 |
|------|------|
| 前端 | Vue 3, TypeScript, Vite, Vue Router 4, Pinia, Tailwind CSS v4, Axios, Zod |
| 後端 | .NET 10, ASP.NET Core Web API, Entity Framework Core |
| 資料庫 | MSSQL 2022 |
| 認證 | JWT + Refresh Token (httpOnly cookie) |
| API 文件 | Scalar / Microsoft.AspNetCore.OpenApi |
| 程式碼品質 | ESLint, Prettier, Vitest, xUnit, Testcontainers.MsSql（整合測試） |
| 基礎設施 | Docker Compose, Nginx, GitHub Actions, Kubernetes |

## 快速啟動

### 前置需求

- Docker Desktop
- make（Windows：[GnuWin32](http://gnuwin32.sourceforge.net/packages/make.htm) 或 WSL）

### 步驟

```bash
# 1. 複製環境變數範本
cp .env.example .env

# 2. 編輯 .env 填入設定（至少修改 DB_PASSWORD 和 JWT_SECRET）

# 3. 啟動所有服務
make dev-build

# 4. 套用資料庫 Migration（首次啟動）
make migrate
```

開啟瀏覽器：
- 應用程式：http://localhost
- API 文件（Scalar）：http://localhost/api/scalar
- 健康檢查：http://localhost/api/health

## 目錄結構

```
website-template-dotnet/
│
├── src/
│   ├── frontend/                        # Vue 3 前端（Vite + TypeScript）
│   └── backend/
│       ├── src/WebTemplate.Api/         # ASP.NET Core Web API 專案
│       │   ├── Modules/                 # 業務模組（每個模組自成一包）
│       │   │   └── Accounts/            # 範例：使用者與認證模組
│       │   │       ├── Controllers/
│       │   │       │   ├── AuthController.cs    # 登入、登出、refresh token 端點
│       │   │       │   └── UsersController.cs   # 使用者 CRUD 端點
│       │   │       ├── Services/
│       │   │       │   ├── Interfaces/
│       │   │       │   │   ├── IAuthService.cs  # JWT 登入、登出、refresh 介面
│       │   │       │   │   ├── IUserService.cs  # 使用者管理介面
│       │   │       │   │   └── ITokenService.cs # Refresh token 管理介面
│       │   │       │   ├── AuthService.cs       # JWT 發行、驗證、refresh 邏輯
│       │   │       │   ├── UserService.cs       # 使用者管理業務邏輯
│       │   │       │   └── TokenService.cs      # Refresh token 管理
│       │   │       ├── Repositories/
│       │   │       │   ├── Interfaces/
│       │   │       │   │   ├── IUserRepository.cs         # 使用者資料存取介面
│       │   │       │   │   └── IRefreshTokenRepository.cs # Refresh token 存取介面
│       │   │       │   ├── UserRepository.cs              # EF Core 使用者查詢實作
│       │   │       │   └── RefreshTokenRepository.cs      # EF Core Token 查詢實作
│       │   │       └── Models/
│       │   │           ├── Entities/
│       │   │           │   ├── User.cs          # EF Core User 實體
│       │   │           │   └── RefreshToken.cs  # EF Core RefreshToken 實體
│       │   │           ├── DTOs/
│       │   │           │   └── AuthDtos.cs      # LoginRequest / RegisterRequest / UserDto
│       │   │           └── Settings/
│       │   │               └── JwtSettings.cs   # JWT 設定類別
│       │   ├── Common/                  # 跨模組共用（不含業務邏輯）
│       │   │   └── Models/
│       │   │       └── ApiResponse.cs   # 統一回傳格式 ApiResponse<T>
│       │   ├── Infrastructure/          # 基礎設施（框架層，不含業務邏輯）
│       │   │   ├── Data/
│       │   │   │   ├── AppDbContext.cs  # EF Core DbContext
│       │   │   │   └── Migrations/     # EF Core 自動產生的 Migration 檔
│       │   │   ├── Extensions/
│       │   │   │   └── ServiceCollectionExtensions.cs  # 所有 DI 注冊集中於此
│       │   │   └── Middleware/
│       │   │       └── GlobalExceptionHandler.cs       # 全域例外攔截
│       │   └── Controllers/
│       │       └── HealthController.cs  # 健康檢查（Liveness / Readiness）
│       └── tests/
│           └── WebTemplate.Api.Tests/   # xUnit 測試（AuthServiceTests 等）
│
├── infra/
│   ├── nginx/                           # Nginx reverse proxy 設定
│   └── k8s/                             # Kubernetes manifests
├── .github/workflows/                   # GitHub Actions CI/CD
├── docs/                                # 架構、部署、開發環境說明文件
└── scripts/                             # 工具腳本（DB seed 等）
```

## 常用指令

```bash
make dev           # 啟動開發環境（前景）
make dev-d         # 啟動開發環境（背景）
make dev-build     # 重新 build 後啟動
make stop          # 停止服務
make clean         # 停止並清除 volumes（慎用！會刪除 DB 資料）
make test          # 執行所有測試
make migrate       # 套用 DB Migration
make migration NAME=AddProductTable  # 新增 Migration
make logs          # 查看即時 logs
make ps            # 查看服務狀態
```

詳細說明請參考 [docs/dev-setup.md](docs/dev-setup.md)。

---

## 分層職責

```
HTTP Request
    ↓
GlobalExceptionHandler       ← 攔截所有未處理例外，回傳標準格式
    ↓
RateLimiter                  ← 速率限制
    ↓
AuthenticationMiddleware     ← JWT 驗證
    ↓
AuthorizationMiddleware      ← 角色權限
    ↓
Controller                   ← 路由、參數驗證、呼叫 Service、回傳格式
    ↓
Service                      ← 業務邏輯（驗證規則、資料組合、計算）
    ↓
Repository                   ← 資料存取（EF Core 查詢，唯一碰 DB 的地方）
    ↓
AppDbContext (EF Core)
    ↓
MSSQL
```

| 層 | 資料夾 | 放什麼 | 不放什麼 |
|----|--------|--------|---------|
| HTTP 層 | `Modules/*/Controllers/` | 路由、參數驗證、呼叫 Service、回傳格式 | 業務邏輯、SQL |
| 業務邏輯 | `Modules/*/Services/` | 驗證規則、資料組合、計算 | 直接查 DB |
| 資料存取 | `Modules/*/Repositories/` | EF Core 查詢、LINQ | 業務判斷 |
| 資料模型 | `Modules/*/Models/Entities/` | EF Core 對應資料表的實體類別 | DTO |
| 請求/回應 | `Modules/*/Models/DTOs/` | Request / Response 的 DTO | 資料庫欄位 |
| 共用 | `Common/Models/` | 跨模組共用（ApiResponse） | 業務邏輯 |
| 基礎設施 | `Infrastructure/` | DB 連線、Middleware、DI 注冊 | 業務邏輯 |

**規則：**
- Controller 不直接碰 Repository
- Repository 不含業務邏輯
- Service 不直接寫 SQL / LINQ

## 共用工具

| 工具 | 位置 | 用途 |
|------|------|------|
| `ApiResponse<T>` | `Common/Models/ApiResponse.cs` | 統一回傳格式（Ok / Fail / Paginated） |
| `AppDbContext` | `Infrastructure/Data/AppDbContext.cs` | EF Core 資料庫上下文 |
| `GlobalExceptionHandler` | `Infrastructure/Middleware/` | 自動攔截未處理例外 |
| `JwtSettings` | `Modules/Accounts/Models/Settings/` | JWT 設定（從 appsettings 取得） |
| `IUserRepository` | `Modules/Accounts/Repositories/Interfaces/` | 使用者資料存取（範本已實作） |
| `IAuthService` | `Modules/Accounts/Services/Interfaces/` | JWT 登入、登出、refresh（範本已實作） |

```csharp
// 統一回傳格式
return Ok(ApiResponse<ProductRes>.Ok(product));
return Ok(ApiResponse<ProductRes>.Fail("查無資料"));
return BadRequest(ApiResponse<ProductRes>.Fail("名稱為必填"));

// 注入已實作的服務
public class MyController(IAuthService auth, IUserRepository users) : ControllerBase { }
```

## 如何擴充這個專案

### 新增一個模組（標準流程）

以新增「Products（商品）」模組為例，完整步驟：

### 步驟 1：建立目錄結構

```
Modules/Products/
├── Controllers/
├── Services/Interfaces/
├── Repositories/Interfaces/
└── Models/
    ├── Entities/
    └── DTOs/
```

### 步驟 2：定義 Models

```csharp
// Modules/Products/Models/Entities/Product.cs
namespace WebTemplate.Api.Modules.Products.Models.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Modules/Products/Models/DTOs/ProductDtos.cs
namespace WebTemplate.Api.Modules.Products.Models.DTOs;

public record ProductRes(Guid Id, string Name, decimal Price);
```

### 步驟 3：定義 Repository 介面與實作

```csharp
// Modules/Products/Repositories/Interfaces/IProductRepository.cs
namespace WebTemplate.Api.Modules.Products.Repositories.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(CancellationToken ct);
    Task<Product?> FindByIdAsync(Guid id, CancellationToken ct);
}

// Modules/Products/Repositories/ProductRepository.cs
namespace WebTemplate.Api.Modules.Products.Repositories;

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<List<Product>> GetAllAsync(CancellationToken ct) =>
        db.Products.ToListAsync(ct);

    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
}
```

### 步驟 4：定義 Service 介面與實作

```csharp
// Modules/Products/Services/Interfaces/IProductService.cs
namespace WebTemplate.Api.Modules.Products.Services.Interfaces;

public interface IProductService
{
    Task<List<ProductRes>> GetAllAsync(CancellationToken ct);
}

// Modules/Products/Services/ProductService.cs
namespace WebTemplate.Api.Modules.Products.Services;

public class ProductService(IProductRepository repo) : IProductService
{
    public async Task<List<ProductRes>> GetAllAsync(CancellationToken ct)
    {
        var products = await repo.GetAllAsync(ct);
        return products.Select(p => new ProductRes(p.Id, p.Name, p.Price)).ToList();
    }
}
```

### 步驟 5：建立 Controller

```csharp
// Modules/Products/Controllers/ProductsController.cs
namespace WebTemplate.Api.Modules.Products.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProductsController(IProductService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var products = await svc.GetAllAsync(ct);
        return Ok(ApiResponse<List<ProductRes>>.Ok(products));
    }
}
```

### 步驟 6：注冊 DI

在 `Infrastructure/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices()` 加入：

```csharp
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<IProductService, ProductService>();
```

### 步驟 7：新增 EF Core Migration

```bash
make migration NAME=AddProductTable
make migrate
```

新模組的 API 會自動出現在 `/api/scalar` 的文件中。

### 寫測試

```bash
# 執行所有測試（後端 + 前端）
make test

# 只跑後端
docker compose exec backend dotnet test

# 只跑前端
cd src/frontend && pnpm test
```

#### 後端測試

測試放在 `src/backend/tests/WebTemplate.Api.Tests/`，參考現有的 `AuthServiceTests.cs` 作為範本：

```csharp
public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly ProductService _svc;

    public ProductServiceTests() => _svc = new ProductService(_repo.Object);

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoProducts()
    {
        _repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync([]);

        var result = await _svc.GetAllAsync(default);

        Assert.Empty(result);
    }
}
```

**測試分層原則：**

| 測試對象 | 測試方式 | 原因 |
|---------|---------|------|
| Service | Unit test（Mock Repository） | 業務邏輯在此，直接驗最有價值 |
| Repository / Controller | 整合測試（`WebApplicationFactory` + Testcontainers.MsSql） | 連真實 SQL Server 容器，確保 EF Core Migration、交易語意、JWT middleware 的整體行為正確；In-memory DB 不支援部分 SQL 語法且無法驗 Migration（見 [ADR-003](docs/adr/ADR-003-testcontainers-for-integration-tests.md)） |

整合測試需本機或 CI 能啟動 Docker（Testcontainers 自動拉起容器並在測試完成後清理）。

```bash
# 只跑單元測試（不需 Docker）
dotnet test --filter Category!=Integration

# 只跑整合測試（需 Docker）
dotnet test --filter Category=Integration
```

一個 `[Fact]` 只驗一件事。

#### 前端測試

```bash
cd src/frontend
pnpm test        # 執行一次
pnpm test:watch  # 監看模式
```

測試放在 `src/frontend/tests/`，目錄結構鏡像 `src/` 下的程式碼位置：

```
tests/
├── setup.ts                    # 測試環境初始化
└── unit/
    └── stores/                 # 對應 src/stores/
        └── auth.test.ts
# 未來依需求建立：
# unit/api/          對應 src/api/（Axios client 行為）
# unit/composables/  對應 src/composables/（組合式函式邏輯）
# components/auth/   對應 src/components/auth/（@vue/test-utils render 測試）
```

> 測試目錄依需求新增，不預先建立空目錄。

---

## 健康檢查

| 端點 | 用途 |
|------|------|
| `GET /api/health` | Liveness — 服務是否存活 |
| `GET /api/health/ready` | Readiness — 資料庫是否可連線 |

> 兩個 image 皆 non-root（backend `app` UID 1000、frontend `nginx`），且 frontend container 對外監聽 8080（k8s Service 仍映射到 80）。生產環境變數（`AllowedHosts` fail-fast、`ConnectionStrings__DefaultConnection`、`Jwt__*`）與 k8s `securityContext` 細節見 [docs/deployment.md](docs/deployment.md)。

## API 文件

開發環境（`ASPNETCORE_ENVIRONMENT=Development`）下，Scalar UI 可在 `/api/scalar` 查看所有 API。
