#!/usr/bin/env bash
# 一鍵初始化開發環境
set -e

echo "=== Website Template - 初始化開發環境 ==="

# 確認 Docker 正在運行
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker 未啟動，請先啟動 Docker Desktop"
    exit 1
fi

# 建立 .env 如果不存在
if [ ! -f ".env" ]; then
    echo "📋 建立 .env 從範本..."
    cp .env.example .env
    echo "⚠️  請編輯 .env 填入你的設定（尤其是 DB_PASSWORD 和 JWT_SECRET）"
    echo "   按 Enter 繼續，或 Ctrl+C 先去編輯 .env"
    read -r
fi

# 啟動服務
echo "🚀 啟動 Docker 服務..."
docker compose up -d --build

# 等待 MSSQL 準備好
echo "⏳ 等待 MSSQL 啟動..."
timeout 60 bash -c 'until docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -Q "SELECT 1" -C -b > /dev/null 2>&1; do sleep 2; done'

# 執行 Migration
echo "🗄️  執行資料庫 Migration..."
docker compose run --rm backend dotnet ef database update --project src/WebTemplate.Api

echo ""
echo "✅ 初始化完成！"
echo ""
echo "   應用程式: http://localhost"
echo "   API 文件:  http://localhost/api/scalar"
echo "   健康檢查:  http://localhost/api/health"
