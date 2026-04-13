# ADR-002：API 回應採用 ApiResponse 信封格式

- **狀態**：Accepted
- **日期**：2026-04-12
- **相關 ADR**：[ADR-001](./ADR-001-module-based-architecture.md)

## 背景

REST API 的回應格式有兩大流派：

1. **HTTP 原生派**：成功直接回傳物件，錯誤用狀態碼 + RFC 7807
2. **信封派（Envelope）**：所有回應包在統一結構中，含 `success`/`data`/`errors`

兩派都有成熟擁護者，選擇需基於生態慣例與實用考量，而非絕對對錯。

本範本需為 .NET 10 後端選定一種並明確記錄理由。

## 決策

**採用 ApiResponse 信封格式**：

```json
// 成功
{
  "success": true,
  "data": { "id": 1, "email": "user@example.com" },
  "message": null,
  "errors": null
}

// 失敗
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": [
    { "field": "email", "message": "Invalid email format" }
  ]
}

// 分頁成功
{
  "success": true,
  "data": [...],
  "message": null,
  "errors": null,
  "meta": { "page": 1, "limit": 20, "total": 150, "totalPages": 8 }
}
```

- 統一由 `Common/Models/ApiResponse.cs` 定義
- 提供 `Ok()`、`Created()`、`Fail()`、`Paginated()` 靜態工廠方法
- HTTP 狀態碼仍反映傳輸層結果（200/400/401/404/500），業務成功/失敗用 `success` 欄位表達

## 考慮過的方案

### 方案 A：HTTP 原生 + RFC 7807
- ✅ 符合 REST 精神
- ✅ 工具鏈友善（curl、Postman、瀏覽器 devtools）
- ✅ Python/Ruby/Go 社群主流
- ❌ **不符合 .NET/Java 企業圈慣例**
- ❌ 前端需為不同狀態碼寫不同 parser
- ❌ 分頁、錯誤等元資料散落在 header 與 body

### 方案 B：ApiResponse 信封（採用）
- ✅ **.NET/Java 企業圈常見**，符合讀者預期
- ✅ 前端永遠用同一個 parser
- ✅ 錯誤結構統一，便於 typed SDK 生成
- ✅ meta 欄位自然容納分頁、版本、追蹤 ID
- ❌ Body 略膨脹
- ❌ HTTP 狀態碼資訊重複出現在 `success` 欄位
- ❌ 與 REST 純粹主義者衝突

### 方案 C：混合（成功原生、錯誤用 RFC 7807）
- ✅ Stripe、GitHub 的做法
- ❌ 前端仍需處理兩種 shape
- ❌ 分頁 meta 需另想辦法
- ❌ 喪失信封的一致性優點

## 採納理由

**範本的價值是示範該生態的最佳實踐，而非追求跨語言一致。**

.NET 開發者對信封格式有成熟的心理模型——從 WCF 時代的 `FaultException`、WebAPI 的 `HttpResponseMessage`，到現代的 `Result<T>` 模式，信封思維深植文化。強行導入 RFC 7807 會讓 .NET 讀者感到「為賦新詞強說愁」。

姊妹專案 `website-template-python` 刻意採用相反決策（[template-python ADR-001](../../../website-template-python/docs/adr/ADR-001-drf-native-response-format.md)），因為 Django/DRF 生態的心理模型完全不同。

**一致性應體現在工程紀律（分層、測試、安全），而非語法層強制統一。**

## 影響範圍

### 程式碼
- `Common/Models/ApiResponse.cs` 定義信封結構與工廠方法
- 所有 Controller 回應皆包裝為 `ApiResponse<T>`
- `Infrastructure/Middleware/GlobalExceptionHandler.cs` 將未處理例外轉換為 `ApiResponse.Fail()`

### 前端契約
- `src/frontend/src/api/client.ts` 統一解析信封
- TypeScript 定義 `ApiResponse<T>` 型別

### 文件
- README.md「API 設計」章節說明信封格式
- `docs/architecture.md` 包含信封 JSON 範例

### 測試
- 所有 Controller 整合測試需驗證 `success`、`data`、`errors` 欄位
- 分頁測試額外驗證 `meta` 結構

## 不採納此決策的情境

若你 fork 本範本但專案屬於以下情境，建議改為方案 A（HTTP 原生 + RFC 7807）：

1. API 面對的是外部開發者，需符合 REST 社群預期
2. 專案需整合大量第三方 API 工具（如 OpenAPI code generators）
3. 團隊以 Python/Ruby/Go 背景為主

此時需同步修改 `ApiResponse.cs`、`GlobalExceptionHandler.cs` 與前端 parser。

## 後續行動

- [x] `ApiResponse<T>` 實作
- [x] 前端 `apiClient` 統一解析
- [x] `GlobalExceptionHandler` 包裝例外
- [ ] OpenAPI schema 生成驗證（確認 `drf-spectacular` 類工具能正確產出信封 schema）

## 參考資料

- [JSend Specification](https://github.com/omniti-labs/jsend)（信封格式的早期規範之一）
- [RFC 7807 - Problem Details](https://datatracker.ietf.org/doc/html/rfc7807)（對立方案的業界標準）
- [template-python ADR-001](../../../website-template-python/docs/adr/ADR-001-drf-native-response-format.md)（姊妹專案的相反決策）
