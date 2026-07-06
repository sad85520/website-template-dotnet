# ADR-004：API 回應改用 HTTP 原生格式 + RFC 7807 Problem Details

- **狀態**：Accepted
- **日期**：2026-07-06
- **相關 ADR**：[ADR-002](./ADR-002-api-response-envelope.md)（本決策取代）

## 背景

[ADR-002](./ADR-002-api-response-envelope.md) 原先採用 `ApiResponse<T>` 信封格式，理由是「示範 .NET/Java 企業圈的慣例心理模型」。實際維運後重新評估，發現這個決策付出的代價比預期高：

1. **每個 Controller action 都要手動包裝**：成功回應要記得呼叫 `ApiResponse<T>.Ok()`，例外處理要記得呼叫 `ApiResponse<T>.Fail()`，容易在新增端點時漏包或包錯層。
2. **與 ASP.NET Core 框架預設行為衝突**：`[ApiController]` 的 model validation 失敗預設會產生 `ValidationProblemDetails`（RFC 7807），要維持信封一致性就必須用 `Configure<ApiBehaviorOptions>` 攔截覆寫，等於捨棄框架已經寫好、與 OpenAPI/Scalar 文件產生器整合良好的行為，換來自己維護一份等價邏輯。
3. **與姊妹專案 `website-template-python` 難以共用前端錯誤處理邏輯**：Python 範本採用 DRF 原生回應格式，兩個範本的前端若要共用同一套 API client / 錯誤處理慣例（例如同一個團隊的全端工程師在專案間切換時），信封格式的差異反而製造額外的心智負擔，而非 ADR-002 原先預期的「示範各生態最佳實踐」。
4. **HTTP 狀態碼與 `success` 欄位語意重複**：呼叫端要嘛只看狀態碼、要嘛只看 `success` 欄位，兩者長期實務下來很少真正互補使用，反而增加序列化欄位與前端型別定義的維護量。

## 決策

**成功回應直接回傳資源本體，不包信封；錯誤回應一律採用 ASP.NET Core 原生的 RFC 7807 `ProblemDetails`（`application/problem+json`）。**

```json
// 成功（200 OK）
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "displayName": "Test User",
  "role": "user",
  "createdAt": "2026-07-06T08:00:00Z"
}

// 錯誤（409 Conflict，業務例外）
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Email is already registered.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}

// 錯誤（400 Bad Request，model validation 失敗，框架原生 ValidationProblemDetails）
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Password": ["The field Password must be a string with a minimum length of 8."]
  },
  "traceId": "00-..."
}
```

- `builder.Services.AddProblemDetails()` + `builder.Services.AddExceptionHandler<GlobalExceptionHandler>()` 註冊 RFC 7807 支援。
- `GlobalExceptionHandler` 攔截 `AppException`（白名單，訊息已審核）轉為對應狀態碼的 `ProblemDetails`；其餘例外一律轉為通用 500，不洩漏內部細節（沿用 ADR-002 時代就有的白名單策略，機制不變，只是輸出格式從 `ApiResponse.Fail()` 換成 `ProblemDetails`）。
- `app.UseStatusCodePages()` 讓沒有拋例外、但回了空狀態碼的路徑（例如 `[Authorize]` fallback 產生的裸 401/403）也補上 ProblemDetails body。
- Model validation 失敗**不再攔截覆寫**，直接沿用框架預設的 `ValidationProblemDetails`。
- 分頁列表端點不再有信封可夾帶 `meta`，改以 `X-Pagination-Total` / `X-Pagination-Page` / `X-Pagination-Limit` / `X-Pagination-Total-Pages` 回應標頭傳遞分頁中繼資料（類比 GitHub API 的 `Link` header、其他 REST API 常見的 `X-Total-Count`）。
- 移除 `Common/Models/ApiResponse.cs`（`ApiResponse<T>`、`FieldError`、`PaginationMeta`）。

## 考慮過的方案

沿用 ADR-002 的方案比較（HTTP 原生 + RFC 7807 / ApiResponse 信封 / 混合），差異在於本次決策後的實際採用結論相反：

### 方案 A：HTTP 原生 + RFC 7807（採用）
- ✅ 符合 REST 精神，工具鏈友善（curl、Postman、瀏覽器 devtools）
- ✅ 與 ASP.NET Core 框架預設行為（`ValidationProblemDetails`、`ProblemDetails`）完全對齊，不需攔截覆寫
- ✅ 與姊妹專案 `website-template-python`（DRF 原生回應格式）的前端錯誤處理慣例可共用
- ❌ 分頁、追蹤 ID 等中繼資料需另尋位置（改走 HTTP header）

### 方案 B：ApiResponse 信封（ADR-002 原決策，已捨棄）
- ✅ meta 欄位可自然容納分頁資訊
- ❌ 每個 action 需手動包裝，容易遺漏或包錯層
- ❌ 與框架原生驗證錯誤格式衝突，需額外程式碼維持一致性
- ❌ 與姊妹專案的前端錯誤處理邏輯無法共用

## 採納理由

**框架原生行為的維護成本，長期低於「示範慣例」帶來的教學價值。**

RFC 7807 是 IETF 標準（RFC 9457 已將其升級為 Standards Track），ASP.NET Core 自 .NET 7 起原生支援且與 model validation、`IExceptionHandler`、Minimal API 的 `TypedResults.Problem()` 全面整合。維持一份平行的 `ApiResponse<T>` 邏輯等於重新發明框架已經做好的事，且每次框架升版都要重新確認兩者是否還一致。

## 影響範圍

### 程式碼
- 刪除 `Common/Models/ApiResponse.cs`
- `AuthController` / `UsersController`：成功回應直接回傳 DTO；`Unauthorized(ApiResponse.Fail(...))` 一類的手動錯誤回應改為拋出對應的 `AppException` 子類別，交給 `GlobalExceptionHandler` 統一轉譯
- `GlobalExceptionHandler`：改用 `IProblemDetailsService.TryWriteAsync` 寫出 `ProblemDetails`
- `Program.cs`：移除 `Configure<ApiBehaviorOptions>` 的信封覆寫，新增 `app.UseStatusCodePages()`

### 前端契約
- `src/frontend/src/api/client.ts`、`src/frontend/src/types/common.ts`：移除 `ApiResponse<T>` 型別，改依 HTTP status code 分流；新增 `ProblemDetails` 介面
- `src/frontend/src/stores/auth.store.ts`、`src/frontend/src/composables/useAuth.ts`：錯誤處理邏輯改為捕捉 Axios 錯誤時解析 `ProblemDetails.detail`/`errors`，成功路徑直接使用回應主體

### 文件
- README.md「API 設計」與相關章節改寫為 RFC 7807 慣例
- `docs/architecture.md` 移除信封 JSON 範例

### 測試
- 所有 Controller 整合測試改為直接反序列化資源本體（成功）或 `ProblemDetails`/`ValidationProblemDetails`（錯誤）

## 參考資料

- [RFC 9457 - Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457)（RFC 7807 的 Standards Track 修訂版）
- [ASP.NET Core: Handle errors](https://learn.microsoft.com/aspnet/core/web-api/handle-errors)（`IProblemDetailsService` 官方文件）
- [ADR-002](./ADR-002-api-response-envelope.md)（本決策取代的原始 ADR，含完整方案比較的歷史脈絡）
