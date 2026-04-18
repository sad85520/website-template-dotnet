.PHONY: dev dev-d dev-build stop clean test lint frontend-test backend-test frontend-lint backend-lint migrate migration ps logs

# 啟動開發環境（前景）
dev:
	docker compose up

# 啟動開發環境（背景）
dev-d:
	docker compose up -d

# 重新 build 後啟動
dev-build:
	docker compose up --build

# 停止所有服務
stop:
	docker compose down

# 停止並清除 volumes（慎用）
clean:
	docker compose down -v --remove-orphans

# 執行前端測試
frontend-test:
	docker compose run --rm frontend pnpm test

# 執行後端測試
backend-test:
	docker compose run --rm backend dotnet test

# 執行所有測試
test: frontend-test backend-test

# 前端 lint
frontend-lint:
	docker compose run --rm frontend pnpm lint

# 後端 format 檢查（對齊 CI 的 dotnet format --verify-no-changes）
backend-lint:
	docker compose run --rm backend dotnet format WebTemplate.sln --verify-no-changes

# 所有 lint
lint: frontend-lint backend-lint

# 執行 EF Core Migration
migrate:
	docker compose run --rm backend dotnet ef database update --project src/WebTemplate.Api

# 新增 EF Core Migration
# 用法: make migration NAME=AddUserTable
migration:
	docker compose run --rm backend dotnet ef migrations add $(NAME) --project src/WebTemplate.Api

# 查看服務狀態
ps:
	docker compose ps

# 查看 logs
logs:
	docker compose logs -f
