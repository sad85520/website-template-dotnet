# website-template-dotnet

現代前後端分離架構網站起手式 Template（.NET 版）。

## 技術棧

| 層級 | 技術 |
|------|------|
| 前端 | Vue 3, TypeScript, Vite, Vue Router 4, Pinia, Tailwind CSS, Axios |
| 後端 | .NET 9, ASP.NET Core Web API, Entity Framework Core |
| 資料庫 | MSSQL 2022 |
| 認證 | JWT + Refresh Token (httpOnly cookie) |
| API 文件 | Swagger / OpenAPI (Swashbuckle) |
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
- API Swagger：http://localhost/api/swagger
- 健康檢查：http://localhost/api/health

## 目錄結構

```
website-template-dotnet/
├── src/
│   ├── frontend/              # Vue 3 前端
│   └── backend/               # .NET 9 後端
│       ├── src/WebTemplate.Api/   # Web API 專案
│       │   ├── Controllers/       # 路由與 HTTP 處理
│       │   ├── Services/          # 商業邏輯
│       │   ├── Models/            # Entities, DTOs, Settings
│       │   └── Data/              # EF Core DbContext + Migrations
│       └── tests/                 # xUnit 測試
├── infra/
│   ├── nginx/                 # Nginx 設定
│   └── k8s/                   # Kubernetes manifests
├── .github/workflows/         # GitHub Actions CI/CD
├── docs/                      # 文件
└── scripts/                   # 工具腳本
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
