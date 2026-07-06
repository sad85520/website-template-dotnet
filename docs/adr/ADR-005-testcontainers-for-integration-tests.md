# ADR-005：整合測試使用 Testcontainers 連真實 SQL Server

- **狀態**：Accepted
- **日期**：2026-07-06
- **相關 ADR**：無

## 背景

範本的整合測試（`Integration/AuthControllerIntegrationTests.cs`）透過 `WebApplicationFactory<Program>` 啟動完整 middleware pipeline，驗證 HTTP 層契約。資料庫層原先改用 **SQLite in-memory**（而非 production 使用的 SQL Server），理由是啟動快、不需要額外依賴。

實務上這個選擇造成兩個問題：

1. **README 與程式碼不一致**：README 一直宣稱整合測試「連真實 SQL Server 容器（Testcontainers.MsSql）」，但程式碼實際是 SQLite in-memory，兩者從未對齊過，衍生專案的開發者會誤信「整合測試已覆蓋真實 DB 行為」。
2. **SQLite 與 SQL Server 的行為差異，會讓测试通過但 production 出錯**：
   - **decimal 精度**：SQLite 對 `decimal`/`numeric` 沒有原生型別，實際以 TEXT 或 REAL 儲存，精度與捨入規則與 SQL Server 的 `decimal(p,s)` 不同。
   - **rowversion / concurrency token**：SQL Server 的 `rowversion` 是資料庫自動遞增的二進位型別，SQLite 沒有對應概念，EF Core 對此欄位的並行衝突偵測在兩種 provider 下行為不同。
   - **collation / 大小寫敏感度**：SQL Server 預設 collation 通常大小寫不敏感，SQLite 預設字串比較大小寫敏感，`WHERE Email = @email` 這類查詢在兩個 provider 下可能得到不同結果。
   - **Migration 完全不會被驗證**：SQLite in-memory 測試多半用 `EnsureCreated()` 直接依 EF Core model 產生 schema，完全繞過 `Migrations/` 資料夾——如果 migration 檔本身寫錯（例如手改過 migration script），測試永遠不會發現。
3. **Docker 已是本範本的硬前提**：`docker-compose.yml` 是主線開發環境，`make dev` / `make test` 都假設 Docker 可用；用 SQLite in-memory 換取「不需要 Docker 就能跑測試」的好處並不成立——本地開發與 CI 都已經要求 Docker 存在。

## 決策

**整合測試改用 Testcontainers.MsSql 啟動真實 SQL Server 容器，測試全程套用真正的 EF Core Migration；container 由整個 test collection 共用一份，測試之間以 Respawn 清空資料表而非各自起新容器。**

### 關鍵設計

1. **單一容器，collection 共用**：`CustomWebApplicationFactory` 實作 `IAsyncLifetime`，作為 `[CollectionDefinition("Integration")]` 的 collection fixture；xUnit 對整個 `Integration` collection 只建立一次容器，而非每個測試類別各自起一個——容器啟動成本（下載 image、啟動 SQL Server、等待就緒）遠高於單純清資料，多起容器沒有實質好處。
2. **套用真正的 Migration**：容器啟動後呼叫 `AppDbContext.Database.MigrateAsync()`，而非 `EnsureCreated()`，讓 `Migrations/` 資料夾內的腳本本身也被驗證。
3. **Respawn 清資料**：每個測試方法執行前（測試類別實作 `IAsyncLifetime.InitializeAsync`）呼叫 `CustomWebApplicationFactory.ResetDatabaseAsync()`，以 Respawn 清空所有資料表（`__EFMigrationsHistory` 排除在外），確保測試之間互不污染，同時避免重複啟動容器的成本。
4. **本機需求**：本機或 CI 執行整合測試需要 Docker 可用；`docker info` 失敗時測試會直接失敗並附上清楚的錯誤訊息（Testcontainers 內建行為），不會誤判為測試邏輯錯誤。

## 考慮過的方案

### 方案 A：維持 SQLite in-memory
- ✅ 啟動快、不需要 Docker
- ❌ 與 production 資料庫行為有實質落差（見上方背景）
- ❌ Migration 腳本本身完全不被驗證
- ❌ 與 README 一直以來的宣稱不符

### 方案 B：Testcontainers.MsSql，每個測試類別各自起一個容器
- ✅ 測試類別之間完全隔離，無需清資料邏輯
- ❌ 容器啟動成本隨測試類別數量線性增加，測試套件數量成長後會明顯拖慢 CI
- ❌ 本範本目前僅一個整合測試類別看不出差異，但作為「後續專案的標準基線」會被複製擴增，需要在一開始就選對可擴展的模式

### 方案 C：Testcontainers.MsSql，整個測試回合共用一個容器 + Respawn 清資料（採用）
- ✅ 容器只啟動一次，新增測試類別不增加容器啟動成本
- ✅ Respawn 清資料速度遠快於重啟容器
- ✅ 真正驗證 Migration 與 SQL Server 特有行為
- ❌ 需要额外引入 Respawn 套件與清資料邏輯（一次性設定成本）

## 採納理由

**範本作為「後續所有專案的標準基線」，整合測試的資料庫行為必須是使用者能夠信任的——文件宣稱測什麼，程式碼就該真的測什麼。** 容器共用 + Respawn 是 Testcontainers 官方文件與社群公認的標準模式（避免「每個測試起一個容器」這個常見的效能陷阱），選它而非各自起一個容器，是為了讓衍生專案在增加更多整合測試類別時不會無痛地把 CI 時間拖垮。

## 影響範圍

### 程式碼
- `tests/WebTemplate.Api.Tests/WebTemplate.Api.Tests.csproj`：新增 `Testcontainers.MsSql`、`Respawn`；移除 `Microsoft.EntityFrameworkCore.Sqlite`
- `Integration/CustomWebApplicationFactory.cs`：改為啟動 `MsSqlContainer`、套用真實 Migration、提供 `ResetDatabaseAsync()`；新增 `IntegrationTestCollection`（`[CollectionDefinition]`）供所有整合測試類別共用
- `Integration/AuthControllerIntegrationTests.cs`：標註 `[Collection(IntegrationTestCollection.Name)]`，實作 `IAsyncLifetime.InitializeAsync` 呼叫 `ResetDatabaseAsync()`
- `Helpers/TestDbContextFactory.cs`：移除（`Services/*Tests.cs` 改為以 Moq 模擬 Repository 介面，不再需要任何真實或 in-memory 資料庫，見下方「單元測試」）

### 單元測試（附帶影響，非本決策核心）
- `AuthServiceTests.cs`、`TokenServiceTests.cs` 原本透過 `TestDbContextFactory`（SQLite in-memory）驗證 Service 邏輯，移除 SQLite 依賴後改為直接 Mock `IUserRepository` / `IRefreshTokenRepository`，成為不依賴任何資料庫的真正單元測試；需要驗證真實資料庫行為（約束、Migration）的部分已由整合測試涵蓋。

### CI
- `.github/workflows/ci.yml` 的 `backend-ci` job 在 `ubuntu-latest` runner 上執行，GitHub Actions 的 ubuntu runner 原生內建 Docker daemon，Testcontainers 可直接使用，不需要額外設定。

### 文件
- README.md 測試分層原則表格的 Testcontainers 描述與程式碼對齊；`docs/adr/ADR-003-testcontainers-for-integration-tests.md` 這個曾被 README 引用但從未存在的檔名，改為本 ADR-005 的正確連結。

## 參考資料

- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Testcontainers.MsSql](https://www.nuget.org/packages/Testcontainers.MsSql)
- [Respawn](https://github.com/jbogard/Respawn)（測試間快速清空資料庫）
