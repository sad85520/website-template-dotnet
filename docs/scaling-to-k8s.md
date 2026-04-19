# 何時從 Compose 升級到 Kubernetes

> **TL;DR**：1–5 服務、單機、小團隊，留在 Compose。當你真的撞到多節點 HA / 自動擴縮 / 專職 DevOps 的牆再升級。

本範本刻意把 Docker Compose 設為預設部署路線，k8s 設為選用。本文說明判斷門檻與遷移步驟。

## 主流判斷點（2026）

留在 Compose：

- 服務總數 1–5 個（frontend / backend / DB / cache 算 1 服務 / 1）
- 單機 VM 撐得住流量（典型 $50–200/月 的 VM 可撐 10K 日活）
- 團隊 1–5 人，沒有專職 DevOps
- 不需要跨節點 HA / 滾動升級 / 自動擴縮

升級到 K8s：

- ≥ 10 個獨立服務，每個有自己的生命週期
- 需要多節點 HA（單一 VM down time 不可接受）
- 流量突刺需要真正的自動擴縮（HPA）
- 合規要求跨 workload 隔離（PSP / PSS、NetworkPolicy）
- 團隊有 ≥ 2 人的專職 DevOps / Platform team

## 成本對照（單位：USD / 月）

| 方案 | 基本配置 | 月費 |
|------|---------|------|
| Docker Compose（單 VM） | 4 vCPU / 8GB RAM | 50–200 |
| 自架 K3s / MicroK8s（單 VM） | 同上，省控制平面費 | 100–300 |
| 管理型 K8s（AKS） | 控制平面免費 + 3 節點 | 300–800 |
| 管理型 K8s（GKE） | 單 zonal cluster 控制平面免費 + 3 節點 | 300–800 |
| 管理型 K8s（EKS） | 控制平面 $73 + 3 節點 | 400–1000 |

實務上 K8s 的**時間成本**比月費更痛：cluster upgrade / node patching / cert rotation / etcd backup / 網路 debug，小團隊約吃一個工程師 20–40% 的時間。

## 升級流程

當專案真的需要 k8s 時，本範本已內含完整的 k8s 配置，升級只需幾步：

### 1. 啟用 k8s workflow

範本內附的 k8s workflow **預設停用**：

- `.github/workflows/cd-k8s-staging.yml` — 預設只允許手動觸發
- `.github/workflows/cd-k8s-production.yml` — 預設只允許手動觸發

要把 staging 升級改成 CI 綠燈後自動觸發，編輯 `cd-k8s-staging.yml` 頂端的 `on:` 區塊，取消 `workflow_run:` 的註解：

```yaml
on:
  workflow_dispatch:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main]
```

### 2. 設定 Branch protection（必要）

`cd-k8s-staging.yml` 與 `cd-k8s-production.yml` 會 commit k8s manifest 回 main。GitHub Repository Settings → Branches → main：

- ✅ Require a pull request before merging
- ✅ Require status checks to pass（至少 CI workflow）
- ✅ 允許 `github-actions[bot]` bypass（或另設 deploy key）

如果不允許 bot bypass，CD 的 `git push` 會被擋下——這是預期行為。

### 3. 套用 k8s manifest

已經驗證過的 manifest 在 [infra/k8s/](../infra/k8s/)：

```bash
kubectl apply -f infra/k8s/namespace.yml
# 先複製 secret.example.yml 填 base64 值
cp infra/k8s/secret.example.yml infra/k8s/secret.yml
kubectl apply -f infra/k8s/secret.yml
kubectl apply -f infra/k8s/configmap.yml
kubectl apply -f infra/k8s/database/
kubectl apply -f infra/k8s/backend/
kubectl apply -f infra/k8s/frontend/
kubectl apply -f infra/k8s/ingress.yml
```

細節參見 [deployment.md 的「Kubernetes 部署」節](deployment.md#kubernetes-部署進階選用)。

## 如果決定不用 k8s

若專案確定不會走 k8s，可以直接刪除以降低 repo 複雜度：

```bash
git rm -r infra/k8s/
git rm .github/workflows/cd-k8s-staging.yml .github/workflows/cd-k8s-production.yml
# 同時清除 docs/deployment.md 的 K8s 相關章節、scaling-to-k8s.md 本檔
```

不刪也沒關係——現行預設下，k8s 工作流不會觸發，manifest 不會被改動，對日常開發零干擾。留著將來真的需要時還有套路。

## 參考

- [Kubernetes is Overkill for 90% of Startups — CodeX/Medium](https://medium.com/codex/kubernetes-is-overkill-for-90-of-startups-just-use-docker-compose-73b561b35c92)
- [Docker Compose vs Kubernetes: When to Use Each in 2026 — DEV](https://dev.to/_d7eb1c1703182e3ce1782/docker-compose-vs-kubernetes-when-to-use-each-in-2026-1hji)
- [Kubernetes Pricing 2026: EKS vs AKS vs GKE — Sedai](https://sedai.io/blog/kubernetes-cost-eks-vs-aks-vs-gke)
