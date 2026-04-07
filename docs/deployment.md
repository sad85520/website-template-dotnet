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

### 更新部署

CD pipeline 會在 merge to main 後自動更新 K8S manifests 中的 image tag。

手動更新：
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
