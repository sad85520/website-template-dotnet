# ADR-003：Refresh Token Rotation + 重用偵測

- **狀態**：Accepted
- **日期**：2026-04-12
- **相關 ADR**：無

## 背景

本範本使用 JWT Bearer 認證，需決定 Refresh Token 的生命週期管理策略。

Refresh Token 的安全性是現代認證系統的關鍵議題：

1. Access Token 生命週期短（15 分鐘），即使洩漏影響有限
2. Refresh Token 生命週期長（7 天以上），一旦洩漏可持續換發新 Access Token
3. XSS、中間人攻擊、惡意程式等均可能竊取 Refresh Token
4. 必須假設「Refresh Token 會被偷」並設計偵測機制

OAuth 2.0 Security Best Current Practice（draft-ietf-oauth-security-topics）明確建議所有公開客戶端採用 Refresh Token Rotation。

## 決策

**採用 Refresh Token Rotation + 重用偵測 + HttpOnly Cookie 儲存。**

### 三項關鍵機制

1. **Rotation**：每次 `/auth/refresh` 呼叫，撤銷舊 token 並發新 token
2. **重用偵測**：若已撤銷的 token 被再次使用，**撤銷該使用者所有 active session**
3. **儲存**：Refresh Token 以 HttpOnly + Secure + SameSite=Strict cookie 儲存，防止 XSS 竊取
4. **雜湊儲存**：DB 僅存 SHA-256 hash，明文永不落地

## 考慮過的方案

### 方案 A：長效 Refresh Token，無 Rotation
- ✅ 實作最簡單
- ❌ Token 洩漏後攻擊者可持續使用直到過期
- ❌ 無法偵測洩漏事件
- ❌ 違反 OAuth 2.0 Security BCP

### 方案 B：Refresh Token Rotation（無重用偵測）
- ✅ 每次使用後舊 token 失效，縮短洩漏窗口
- ❌ 攻擊者若在合法使用者之前呼叫 refresh，合法使用者下次呼叫時失敗，但系統**不知道這是攻擊**，只會登出合法使用者
- ❌ 無法自動回應洩漏事件

### 方案 C：Rotation + 重用偵測（採用）
- ✅ 攻擊者使用已撤銷的 token 會**觸發重用偵測**
- ✅ 系統自動撤銷該使用者所有 session，迫使所有裝置重新登入
- ✅ 合法使用者獲得明確的登出訊號，可察覺異常
- ✅ 符合 OAuth 2.0 Security BCP § 4.13
- ❌ DB 需保留被撤銷的 token 以供偵測
- ❌ 需設計 token 鏈結構（new token 指向 previous token id）

### 儲存位置

**方案 D：Refresh Token 存 LocalStorage**
- ✅ 前端取用簡單
- ❌ **XSS 可直接竊取**（critical）
- ❌ 違反 OWASP 建議

**方案 E：Refresh Token 存 HttpOnly Cookie（採用）**
- ✅ JavaScript 無法讀取，XSS 免疫
- ✅ 配合 Secure + SameSite=Strict 進一步防 CSRF
- ❌ 需處理 CORS 與 cookie 設定的複雜度

## 採納理由

對於生產級範本，安全預設必須採用業界最佳實踐。Refresh Token Rotation + 重用偵測是 OAuth 2.0 Security BCP 的明確建議，**沒有理由為簡化而妥協**。

HttpOnly Cookie 儲存是唯一能防止 XSS 竊取 Refresh Token 的方案。LocalStorage 因方便而流行，但在安全層面不可接受。

## 影響範圍

### 資料模型
```csharp
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; }      // SHA-256 of raw token
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? ReplacedByTokenId { get; set; } // 形成 rotation chain
    public string? RevocationReason { get; set; } // "rotated", "reuse_detected", "logout"
}
```

### 核心流程

**發放（Login）**
1. 生成 `rawToken` = cryptographically secure random (32 bytes)
2. DB 儲存 `SHA256(rawToken)`
3. HttpOnly cookie 回傳 `rawToken`

**換發（Refresh）**
1. 從 cookie 取 `rawToken`，計算 `SHA256(rawToken)` 比對 DB
2. **若 token 已被撤銷** → 觸發重用偵測，撤銷該使用者所有 token，回 401
3. 若未撤銷 → 撤銷舊 token（`RevokedAt = now`, reason=`"rotated"`），發新 token，建立鏈結

**登出（Logout）**
- 撤銷當前 token（reason=`"logout"`），清除 cookie

### 程式碼位置
- `Modules/Accounts/Services/TokenService.cs` 核心邏輯
- `Modules/Accounts/Services/AuthService.cs` 重用偵測觸發
- `Modules/Accounts/Repositories/RefreshTokenRepository.cs` 資料存取
- `Modules/Accounts/Controllers/AuthController.cs` Cookie 設定

### Cookie 設定
```csharp
Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
{
    HttpOnly = true,
    Secure = true,                              // 僅 HTTPS
    SameSite = SameSiteMode.Strict,             // 防 CSRF
    Expires = DateTimeOffset.UtcNow.AddDays(7),
    Path = "/api/auth"                          // 縮小範圍
});
```

### 關鍵實作細節

**EF Core Change Tracker 陷阱**（位於 `TokenService.cs`）：

```csharp
// 錯誤：直接設定原始 token 到 entity，EF 會把原始 token 寫回 DB
refreshToken.TokenHash = rawToken;
await context.SaveChangesAsync();

// 正確：Detach 後再設定，只作為回傳載體
refreshTokenRepository.Detach(refreshToken);
refreshToken.TokenHash = rawToken; // DB 仍保持 hash
return refreshToken;
```

此坑需在程式碼中加註解警示。

## 後續行動

- [x] `RefreshToken` entity 設計
- [x] Rotation 邏輯實作
- [x] 重用偵測實作
- [x] HttpOnly Cookie 設定
- [x] SHA-256 雜湊儲存
- [ ] 監控指標：重用偵測觸發次數（應接近零，非零代表有攻擊或 bug）
- [ ] 定期清理過期且已撤銷的 RefreshToken 紀錄（避免表無限增長）

## 參考資料

- [OAuth 2.0 Security Best Current Practice §4.13](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)
- [Auth0: Refresh Token Rotation](https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation)
- [OWASP JWT Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
