# 部署指南

## Docker Compose 生產部署

```bash
# 1. 設定環境變數
cp .env.example .env
# 填入生產環境的 DB_PASSWORD 和 JWT_SECRET

# 2. 啟動生產環境
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# 3. 執行 Migration（僅首次或有新 migration 時）
docker compose run --rm backend dotnet ef database update --project src/WebTemplate.Api
```

## 容器安全與連接埠

兩個 image 皆以 **non-root** 執行，且 frontend container 對外監聽 **8080**，不是 80：

| Image | 執行使用者 | 容器埠 | Compose 映射到宿主 | 備註 |
|-------|-----------|--------|-------------------|------|
| backend | `app` (UID 1654，base image 內建) | 8080 | nginx 反代 | Kestrel 綁 `http://+:8080`；k8s readinessProbe 監 `/api/health/ready` |
| frontend | `nginx`（非 root） | 8080 | nginx 反代 | 改以 8080 讓 non-root 能 bind（1-1023 需 CAP_NET_BIND_SERVICE） |
| nginx (reverse proxy) | root | 80 | 80 | 對外入口仍是 `http://localhost` |

K8s `frontend-service` 以 `port: 80 → targetPort: 8080` 做轉發；對外 URL 不受影響。`backend` / `frontend` Deployment 皆帶 `securityContext.runAsNonRoot: true` 與 `capabilities.drop: [ALL]`；新增 Deployment 請沿用這份 template 以避免 PodSecurity `restricted` policy 拒絕調度。

## 生產 compose 拓撲

`docker-compose.prod.yml` 是 base 的 override，以下幾個拓撲點易踩雷：

- **`volumes: !reset []`**：compose v2 對 list 欄位採累加合併，單寫 `volumes: []` 不會移除 base 的 `./src/backend:/app` bind mount — 會把 image 內發佈好的 `/app`（含 dll）被源碼 mount 蓋掉，runtime 會找不到執行檔。必須用 `!reset []` 才會真的清空，frontend 同理。
- **frontend 不對外發佈 port**：base 的 `5173:5173` 是 dev 的 Vite dev server；prod 的 frontend image 是 nginx，聽 `:8080`。overlay 用 `ports: !reset []` 拔掉公開 port — 外部流量一律走 outer nginx 的 `:80`。
- **Outer nginx 是唯一入口**：`infra/nginx/nginx.conf` 的 `location /` 以 `proxy_pass http://frontend:8080` 反代，**不從本地磁碟 serve 檔案**（容器內 `/usr/share/nginx/html` 刻意留空）。SPA fallback 與靜態快取由 frontend image 自身的 `nginx.spa.conf` 負責，避免兩層 nginx 對同一組 header 重複設定。

## 生產環境變數

ASP.NET Core 設定以環境變數覆寫 `appsettings.json` 同名 key（`__` 代表 JSON 巢狀）。**`AllowedHosts` 在 production 務必透過環境變數設定**，`appsettings.Production.json` 刻意不提供 default，漏設會被 framework 拒絕（回 400）。

| 變數 | 必填 | 用途 |
|------|------|------|
| `AllowedHosts` | 是 | 分號分隔，例 `api.example.com;www.example.com` |
| `ConnectionStrings__DefaultConnection` | 是 | MSSQL 連線字串；k8s 放 Secret |
| `Jwt__Secret` | 是 | JWT 簽章金鑰，至少 32 字元；k8s 放 Secret |
| `Jwt__Issuer` / `Jwt__Audience` | 是 | 識別 token 發行者/目標 |
| `ASPNETCORE_ENVIRONMENT` | 是 | 生產設 `Production` |
| `Serilog__MinimumLevel__Default` | 否 | 臨時壓低 log 雜訊（例：`Warning`） |

K8s ConfigMap/Secret 的分配見 `infra/k8s/configmap.yml` 與 `infra/k8s/secret.example.yml`。

## Kubernetes 部署

### 前置準備

```bash
# 1. 建立 namespace
kubectl apply -f infra/k8s/namespace.yml

# 2. 建立 Secret（從範本複製並填入 base64 值）
cp infra/k8s/secret.example.yml infra/k8s/secret.yml
# 編輯 secret.yml，填入 base64 編碼的值
# echo -n "your-password" | base64
kubectl apply -f infra/k8s/secret.yml

# 3. 套用所有設定
kubectl apply -f infra/k8s/configmap.yml
```

### 部署應用程式

```bash
# 部署所有服務
kubectl apply -f infra/k8s/database/
kubectl apply -f infra/k8s/backend/
kubectl apply -f infra/k8s/frontend/
kubectl apply -f infra/k8s/ingress.yml

# 查看狀態
kubectl get pods -n web-template
kubectl get services -n web-template
```

### CD 部署流程

| 階段 | 觸發方式 | Workflow |
|------|---------|---------|
| Staging | push to main 自動觸發 | `cd.yml` |
| Production | 手動觸發（QA 驗收通過後） | `cd-production.yml` |

**手動觸發 Production 部署：**
```bash
# 指定 SHA（推薦，確保部署正確版本）
gh workflow run cd-production.yml -f sha=<short-sha>

# 使用最新版本
gh workflow run cd-production.yml
```

也可在 GitHub Actions 頁面 → 選擇「CD — Deploy Production」→ 點「Run workflow」。

手動更新 K8S image：
```bash
kubectl set image deployment/backend backend=ghcr.io/your-org/website-template-dotnet/backend:sha-abc1234 -n web-template
kubectl set image deployment/frontend frontend=ghcr.io/your-org/website-template-dotnet/frontend:sha-abc1234 -n web-template
```

## GitHub Actions 設定

在 GitHub Repository Settings → Secrets 新增：

| Secret | 說明 |
|--------|------|
| `GITHUB_TOKEN` | 自動提供，用於 GHCR push |

GHCR（GitHub Container Registry）使用 `GITHUB_TOKEN` 不需要額外設定。

## 修改 K8S Ingress 域名

編輯 [infra/k8s/ingress.yml](../infra/k8s/ingress.yml)，將 `app.example.com` 替換為你的實際域名。
