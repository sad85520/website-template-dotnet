using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            // Email 唯一索引在資料庫層強制執行，防止 service 層的 ExistsByEmailAsync
            // 在高並發下因競態條件（race condition）而重複插入相同 Email。
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            // Role 以字串儲存而非整數，避免日後新增 enum 值時因 ordinal 位移造成資料錯誤。
            entity.Property(u => u.Role).HasConversion<string>();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);
            // TokenHash 儲存的是 SHA-256 雜湊值，唯一索引同時保障查詢效能與不重複性；
            // 原始 token 僅在產生當下回傳給客戶端，資料庫中永不存放明文。
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.Property(r => r.TokenHash).HasMaxLength(512).IsRequired();
            entity.HasOne(r => r.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(r => r.UserId)
                  // Cascade delete：刪除使用者時同步清除所有 refresh token，
                  // 避免孤立的 token 留在資料庫中佔用空間或造成安全疑慮。
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
