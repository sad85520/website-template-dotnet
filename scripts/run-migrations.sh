#!/usr/bin/env bash
# 執行 EF Core Migrations
set -e

echo "=== 執行 EF Core Migrations ==="
docker compose run --rm backend dotnet ef database update --project src/WebTemplate.Api
echo "✅ Migration 完成"
