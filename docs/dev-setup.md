# 開發環境設定指南

## 前置需求

| 工具 | 版本 | 用途 |
|------|------|------|
| Docker Desktop | 4.x+ | 執行所有服務 |
| make | any | 指令捷徑（Windows 建議 WSL 或 GnuWin32） |
| Git | 2.x+ | 版本控制 |

可選（本地開發不透過 Docker）：
- Node.js 22+, pnpm 9+
- .NET SDK 10.0+

## 快速啟動

```bash
# 1. Clone 專案
git clone <repo-url>
cd website-template-dotnet

# 2. 設定環境變數
cp .env.example .env
# 編輯 .env，至少修改 DB_PASSWORD 和 JWT_SECRET

# 3. 初始化前端 lockfile（首次，確保 CI --frozen-lockfile 可正常運行）
cd src/frontend && pnpm install && cd ../..

# 4. 啟動服務（首次較慢，需要 pull images）
make dev-build

# 5. 套用 DB Migration（另開終端）
make migrate
```

## 服務端點

| 服務 | URL |
|------|-----|
| 應用程式 | http://localhost |
| API 文件（Scalar） | http://localhost/api/scalar |
| 健康檢查 | http://localhost/api/health |
| 健康檢查（詳細） | http://localhost/api/health/ready |
| Vite Dev Server（直接） | http://localhost:5173 |
| .NET API（直接） | http://localhost:8080 |
| MSSQL | localhost:1433 |

## 常用指令

```bash
make dev          # 啟動開發環境（前景）
make dev-d        # 啟動開發環境（背景）
make dev-build    # 重新 build 後啟動
make stop         # 停止服務
make clean        # 停止並清除 volumes（慎用！會刪除 DB 資料）
make test         # 執行所有測試
make migrate      # 套用 EF Core Migration
make migration NAME=AddProductTable  # 新增 Migration
make logs         # 查看即時 logs
make ps           # 查看服務狀態
```

## 新增 EF Core Migration

```bash
# 方法 1：透過 Docker Compose
make migration NAME=AddYourTableName

# 方法 2：本地 dotnet CLI
cd src/backend
dotnet ef migrations add AddYourTableName --project src/WebTemplate.Api
```

## 環境變數說明

| 變數 | 必填 | 說明 |
|------|------|------|
| `DB_PASSWORD` | 是 | MSSQL SA 密碼，須包含大小寫字母、數字、特殊字元 |
| `JWT_SECRET` | 是 | JWT 簽署金鑰，至少 32 字元 |
| `ASPNETCORE_ENVIRONMENT` | 否 | 預設 Development |
| `AllowedHosts` | 生產必填 | 分號分隔（例：`api.example.com;www.example.com`）；未設即 400 |

> 生產覆寫慣例（`ConnectionStrings__DefaultConnection` / `Jwt__Secret` / `Jwt__Issuer` / `Jwt__Audience` / `Serilog__*`）與容器 non-root、8080 連接埠、k8s securityContext 細節見 [deployment.md](deployment.md#生產環境變數)。
