# 架構說明

## 整體架構

```
                    ┌─────────────┐
                    │   Browser   │
                    └──────┬──────┘
                           │ HTTP/HTTPS
                    ┌──────▼──────┐
                    │    Nginx    │  統一入口 (port 80/443)
                    └──┬──────┬──┘
              /        │      │  /api/*
     ┌────────▼───┐    │  ┌───▼────────────┐
     │  Frontend  │    │  │    Backend     │
     │  (Vue SPA) │    │  │  (.NET 10 API) │
     │  port 80   │    │  │   port 8080    │
     └────────────┘    │  └───────┬────────┘
                       │          │
                       │   ┌──────▼──────┐
                       │   │    MSSQL    │
                       │   │  port 1433  │
                       │   └─────────────┘
                       │
              (開發環境) Vite Dev Server port 5173
```

## 後端分層

```
HTTP Request
    ↓
SecurityHeadersMiddleware    ← 安全標頭（CSP, HSTS, X-Frame-Options...）
    ↓
GlobalExceptionHandler       ← 全域例外攔截（IExceptionHandler）
    ↓
RateLimiterMiddleware        ← 速率限制
    ↓
AuthenticationMiddleware     ← JWT 驗證
    ↓
AuthorizationMiddleware      ← 角色權限
    ↓
Controller                   ← HTTP 關注點（路由、參數、回傳）
    ↓
Service                      ← 商業邏輯
    ↓
AppDbContext (EF Core)       ← 資料存取
    ↓
MSSQL
```

## JWT 認證流程

```
1. 登入
   POST /api/v1/auth/login
   → 後端回傳: { accessToken } (JSON) + refreshToken (httpOnly cookie)
   → 前端: accessToken 存入 Pinia store（記憶體）

2. 一般請求
   GET /api/v1/users/me
   → 前端 Axios interceptor 自動加入 Authorization: Bearer {accessToken}

3. Access Token 過期（15 分鐘後）
   → 後端回傳 401
   → Axios interceptor 攔截
   → 自動呼叫 POST /api/v1/auth/refresh（cookie 自動帶入 refreshToken）
   → 後端回傳新的 accessToken + rotate refreshToken
   → 重送原始請求

4. 登出
   POST /api/v1/auth/logout
   → 後端撤銷 refreshToken（DB 標記 RevokedAt）
   → 清除 refreshToken cookie
   → 前端清除 Pinia store
```

## Refresh Token 安全機制

- Refresh token 以 SHA-256 hash 儲存於 DB，不儲存原始值
- 每次 refresh 時舊 token 立即撤銷（rotation）
- 偵測到已撤銷的 token 被重複使用時，撤銷該用戶所有 session（可能遭竊）
- httpOnly cookie 防止 XSS 攻擊讀取 token

## 前端狀態管理

```
Pinia Stores:
├── auth    ← accessToken, isAuthenticated, login/logout/refresh
├── user    ← currentUser profile
├── ui      ← sidebar, theme, globalLoading
└── notification ← toast 通知佇列
```
