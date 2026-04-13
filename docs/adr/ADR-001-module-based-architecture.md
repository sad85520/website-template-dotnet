# ADR-001：Module-based 架構 + Repository 抽象層

- **狀態**：Accepted
- **日期**：2026-04-12
- **相關 ADR**：[ADR-002](./ADR-002-api-response-envelope.md)

## 背景

本範本為 .NET 10 Web API 起手式，目標讓新團隊 clone 後能快速建立生產級後端。後端架構需決定：

1. 程式碼組織：按**技術類型**（Controllers/、Services/、Repositories/ 各自一個頂層目錄）還是按**業務模組**（Modules/Accounts/、Modules/Products/ 各自內含完整分層）？
2. 資料存取：是否在 EF Core 之上再包一層 Repository 抽象？

這兩個決策在 .NET 社群都有分歧意見，需明確記錄採納方向與代價。

## 決策

### 1. 採用 Module-based 目錄結構

```
src/backend/src/WebTemplate.Api/
├── Modules/
│   └── Accounts/
│       ├── Controllers/AuthController.cs
│       ├── Services/AuthService.cs, TokenService.cs, UserService.cs
│       ├── Repositories/UserRepository.cs, RefreshTokenRepository.cs
│       └── Models/
│           ├── Entities/User.cs, RefreshToken.cs
│           └── DTOs/AuthDtos.cs
├── Common/                    # 跨模組共用
├── Infrastructure/            # DI、DbContext、Middleware
└── Program.cs
```

每個模組**自包含**完整分層，可獨立理解、測試、甚至拆分為微服務。

### 2. 採用 Repository 抽象層

每個 Entity 對應一個 `IXxxRepository` 介面與 `XxxRepository` 實作。Service 層透過介面操作資料，不直接接觸 `DbContext`。

## 考慮過的方案

### 架構：Layered vs Module-based

**方案 A：Layered（按技術類型）**
```
Controllers/AuthController.cs, UserController.cs
Services/AuthService.cs, UserService.cs
Repositories/UserRepository.cs
Models/User.cs
```
- ✅ .NET 社群最常見
- ✅ 新人立刻看懂
- ❌ 模組間界線模糊，隨專案成長容易產生橫向耦合
- ❌ 拆微服務時需大量搬檔案
- ❌ Code review 時難以一眼看出「這個 PR 動了哪個業務模組」

**方案 B：Module-based（採用）**
- ✅ 業務邊界清晰，符合 DDD 限界上下文思維
- ✅ 未來拆微服務成本低
- ✅ 新增模組的流程可標準化（README 有七步教學）
- ✅ Code review 一眼看出範圍
- ❌ 新人需適應「不是按技術類型找檔案」
- ❌ 跨模組共用需明確透過 `Common/` 或介面

### 資料存取：Repository 抽象層

**方案 C：直接用 DbContext**
- ✅ 省去一層抽象
- ✅ EF Core 本身已是 Repository 模式的實作
- ❌ Service 層直接依賴 EF Core，難以測試
- ❌ 查詢邏輯散落在各 Service 中，重複率高

**方案 D：Repository 抽象層（採用）**
- ✅ Service 層僅依賴介面，mock 容易
- ✅ 查詢邏輯集中，避免散落
- ✅ 未來若切換 ORM（如改用 Dapper），Service 層不受影響
- ✅ 可在 Repository 層加入 cache、audit 等橫切關注
- ❌ 多一層樣板程式碼
- ❌ Django/Ruby 社群視為過度設計（但 .NET 社群普遍接受）

## 採納理由

本範本的目標使用者是「**即將起新專案的中型團隊**」。這類團隊通常：

1. 有多個業務模組（非單一功能）
2. 預期專案會持續擴充 12 個月以上
3. 可能在未來考慮拆微服務
4. 重視可測試性

Module-based 讓團隊從第一天就建立「模組邊界」的思維紀律；Repository 層讓測試友善度最大化。兩個選擇都是「前期多寫一點，後期省很多」的投資。

**但這不是普世最佳解**。對於：

- 極小型專案（< 3 個模組）：Layered 更直接
- Django / Rails 專案：ORM 自帶 Manager/Active Record，Repository 為過度設計（見 template-python 的 ADR-002）

## 影響範圍

### 目錄結構
- 新增模組時必須建立完整的 `Modules/Xxx/{Controllers, Services, Repositories, Models}` 結構
- README.md 的「新增模組」章節有完整七步教學

### DI 註冊
- 所有 Repository 與 Service 在 `ServiceCollectionExtensions.AddApplicationServices()` 集中註冊
- 介面綁定實作：`services.AddScoped<IUserRepository, UserRepository>()`

### 測試
- Service 單元測試 mock `IXxxRepository`
- Repository 整合測試使用 `TestDbContextFactory` 的 InMemory DB
- Controller 整合測試使用 `WebApplicationFactory<Program>`

### Trade-off 承認
- 新人第一次接觸需 20-30 分鐘適應
- 檔案數量較 Layered 架構多約 40%

## 後續行動

- [x] README.md 新增模組的七步教學
- [x] `docs/architecture.md` 繪製模組邊界圖
- [ ] 若本範本實際使用中發現 Repository 層帶來明顯負擔，考慮發布 ADR-004 推翻此決策

## 參考資料

- [Microsoft Learn: Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Domain-Driven Design: Bounded Contexts](https://martinfowler.com/bliki/BoundedContext.html)
- `website-template-python` 的 ADR-002（同一議題在 Django 生態的相反結論）
