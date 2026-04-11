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
| 程式碼品質 | ESLint, Prettier, Vitest, xUnit |
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
│       │   ├── Controllers/
│       │   │   ├── AuthController.cs    # 登入、登出、refresh token 端點
│       │   │   ├── UsersController.cs   # 使用者 CRUD 端點
│       │   │   └── HealthController.cs  # 健康檢查（Liveness / Readiness）
│       │   ├── Services/
│       │   │   ├── Interfaces/          # 業務邏輯介面
│       │   │   ├── AuthService.cs       # JWT 發行、驗證、refresh 邏輯
│       │   │   ├── UserService.cs       # 使用者管理業務邏輯
│       │   │   └── TokenService.cs      # Refresh token 管理
│       │   ├── Repositories/
│       │   │   ├── IUserRepository.cs       # 使用者資料存取介面
│       │   │   ├── UserRepository.cs        # EF Core 使用者查詢實作
│       │   │   ├── IRefreshTokenRepository.cs
│       │   │   └── RefreshTokenRepository.cs
│       │   ├── Models/
│       │   │   ├── Entities/            # EF Core 資料庫實體（User, RefreshToken）
│       │   │   ├── DTOs/                # Request / Response DTO（Auth/, Common/）
│       │   │   └── Settings/            # 設定類別（JwtSettings）
│       │   ├── Data/
│       │   │   ├── AppDbContext.cs       # EF Core DbContext
│       │   │   └── Migrations/          # EF Core 自動產生的 Migration 檔
│       │   ├── Extensions/
│       │   │   └── ServiceCollectionExtensions.cs  # 所有 DI 注冊集中於此
│       │   └── Middleware/
│       │       └── GlobalExceptionHandler.cs       # 全域例外攔截
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
| HTTP 層 | `Controllers/` | 路由、參數驗證、呼叫 Service、回傳格式 | 業務邏輯、SQL |
| 業務邏輯 | `Services/` | 驗證規則、資料組合、計算 | 直接查 DB |
| 資料存取 | `Repositories/` | EF Core 查詢、LINQ | 業務判斷 |
| 資料模型 | `Models/Entities/` | EF Core 對應資料表的實體類別 | DTO |
| 請求/回應 | `Models/DTOs/` | Request / Response 的 DTO | 資料庫欄位 |
| 基礎設施 | `Extensions/`, `Middleware/` | DI 注冊、Middleware | 業務邏輯 |

**規則：**
- Controller 不直接碰 Repository
- Repository 不含業務邏輯
- Service 不直接寫 SQL / LINQ

## 共用工具

| 工具 | 位置 | 用途 |
|------|------|------|
| `ApiResponse<T>` | `Models/DTOs/Common/` | 統一回傳格式（Success / Fail） |
| `AppDbContext` | `Data/AppDbContext.cs` | EF Core 資料庫上下文 |
| `GlobalExceptionHandler` | `Middleware/` | 自動攔截未處理例外 |
| `JwtSettings` | `Models/Settings/JwtSettings.cs` | JWT 設定（從 appsettings 取得） |
| `IUserRepository` | `Repositories/` | 使用者資料存取（範本已實作） |
| `IAuthService` | `Services/Interfaces/` | JWT 登入、登出、refresh（範本已實作） |

```csharp
// 統一回傳格式
return Ok(ApiResponse<ProductRes>.Success(product));
return Ok(ApiResponse<ProductRes>.Fail("查無資料"));
return BadRequest(ApiResponse<ProductRes>.Fail("名稱為必填"));

// 注入已實作的服務
public class MyController(IAuthService auth, IUserRepository users) : ControllerBase { }
```

## 如何擴充這個專案

### 新增一個 API 端點（快速流程）

以新增「取得產品列表」為例：

**1. 建立 Entity**（如果需要新資料表）
```csharp
// Models/Entities/Product.cs
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

**2. 建立 DTO**
```csharp
// Models/DTOs/Products/ProductRes.cs
public record ProductRes(Guid Id, string Name, decimal Price);
```

**3. 新增 Repository**
```csharp
// Repositories/IProductRepository.cs
public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
}

// Repositories/ProductRepository.cs
public class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<List<Product>> GetAllAsync(CancellationToken ct) =>
        db.Products.ToListAsync(ct);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
}
```

**4. 新增 Service**
```csharp
// Services/Interfaces/IProductService.cs
public interface IProductService
{
    Task<List<ProductRes>> GetAllAsync(CancellationToken ct);
}

// Services/ProductService.cs
public class ProductService(IProductRepository repo) : IProductService
{
    public async Task<List<ProductRes>> GetAllAsync(CancellationToken ct)
    {
        var products = await repo.GetAllAsync(ct);
        return products.Select(p => new ProductRes(p.Id, p.Name, p.Price)).ToList();
    }
}
```

**5. 建立 Controller**
```csharp
// Controllers/ProductsController.cs
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProductsController(IProductService svc) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductRes>>>> GetAll(CancellationToken ct)
    {
        var products = await svc.GetAllAsync(ct);
        return Ok(ApiResponse<List<ProductRes>>.Success(products));
    }
}
```

**6. 注冊 DI**（`Extensions/ServiceCollectionExtensions.cs`）
```csharp
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<IProductService, ProductService>();
```

**7. 新增 EF Core Migration**
```bash
make migration NAME=AddProductTable
make migrate
```

### 新增大型功能域（跨多個資料表的模組）

若功能夠大（例如「訂單管理」涵蓋多個資料表與複雜邏輯），在各層的資料夾內建立子目錄保持整潔：

**1. 建立標準目錄結構**
```
Controllers/Orders/
  OrdersController.cs
  OrderItemsController.cs
Services/
  Interfaces/IOrderService.cs
  Interfaces/IOrderItemService.cs
  OrderService.cs
  OrderItemService.cs
Repositories/
  IOrderRepository.cs
  IOrderItemRepository.cs
  OrderRepository.cs
  OrderItemRepository.cs
Models/
  Entities/Order.cs
  Entities/OrderItem.cs
  DTOs/Orders/OrderReq.cs
  DTOs/Orders/OrderRes.cs
```

**2. 在 `Extensions/ServiceCollectionExtensions.cs` 加入 DI 注冊**
```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddScoped<IOrderItemRepository, OrderItemRepository>();
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IOrderItemService, OrderItemService>();
```

**3. 建立 Migration**
```bash
make migration NAME=AddOrderTables
make migrate
```

路由由 Controller 上的 `[Route]` attribute 自動生效，不需額外設定。

### 寫測試

```bash
# 執行所有後端測試
make test

# 只跑後端（不跑前端）
docker compose exec backend dotnet test
```

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

**原則：**
- 測試 Service 層邏輯，Repository 用 Mock 取代
- Repository 若需測試，使用整合測試（`WebApplicationFactory`）
- 一個 `[Fact]` 只驗一件事

---

## 健康檢查

| 端點 | 用途 |
|------|------|
| `GET /api/health` | Liveness — 服務是否存活 |
| `GET /api/health/ready` | Readiness — 資料庫是否可連線 |

## API 文件

開發環境（`ASPNETCORE_ENVIRONMENT=Development`）下，Scalar UI 可在 `/api/scalar` 查看所有 API。
