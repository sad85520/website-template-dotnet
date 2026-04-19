using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Common.Exceptions;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Models.Mappings;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Services;

/// <summary>認證服務實作，處理登入、註冊、Token 刷新與登出邏輯，實作 <see cref="IAuthService"/>。</summary>
public class AuthService(IUserRepository userRepository, ITokenService tokenService) : IAuthService
{
    /// <inheritdoc/>
    public async Task<(UserDto User, string AccessToken, string RefreshToken)> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        // 找不到使用者時回傳與密碼錯誤相同的訊息，
        // 防止攻擊者透過不同錯誤訊息枚舉（enumerate）已存在的帳號。
        // 查詢前先 normalize（lower + trim），確保「Alice@Example.com」與 register 時存入的
        // 「alice@example.com」能比對到同一筆 — 不然使用者改變輸入大小寫就會變成查不到帳號。
        var normalized = NormalizeEmail(request.Email);
        var user = await userRepository.FindByEmailAsync(normalized, ct)
            ?? throw new AppAuthenticationException("Invalid email or password.");

        if (user.LockoutUntil > DateTime.UtcNow)
            throw new AppAuthenticationException("Account is temporarily locked. Try again later.");

        // 密碼驗證失敗時遞增計數並在達到閾值時鎖定帳號，
        // 實作軟性帳號鎖定（soft lockout）以對抗暴力破解。
        // 注意：此處的計數與鎖定邏輯非原子操作，高並發場景下可能略有偏差，
        // 但對於登入嘗試保護已足夠，嚴格場景可改用 Redis 原子計數器。
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
            await userRepository.UpdateAsync(user, ct);
            throw new AppAuthenticationException("Invalid email or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        await userRepository.UpdateAsync(user, ct);

        var accessToken = tokenService.GenerateAccessToken(user);
        var tokenPair = await tokenService.CreateRefreshTokenAsync(user.Id, ct);

        // RawToken 是給客戶端寫入 HttpOnly cookie 的明文；DB 端只存 SHA-256 雜湊
        // （tokenPair.Entity.TokenHash），避免欄位語意混用。
        return (user.ToDto(), accessToken, tokenPair.RawToken);
    }

    /// <inheritdoc/>
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Normalize 一次即可：後續的 ExistsByEmailAsync 與 User.Email 都使用同一字串，
        // 避免 DB 層因大小寫差異而把「Alice@」與「alice@」當成不同使用者造成重複註冊。
        var normalizedEmail = NormalizeEmail(request.Email);

        var exists = await userRepository.ExistsByEmailAsync(normalizedEmail, ct);
        if (exists)
            throw new AppConflictException("Email is already registered.");

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
        };

        try
        {
            await userRepository.CreateAsync(user, ct);
        }
        // ExistsByEmailAsync 與 CreateAsync 之間存在 TOCTOU 空窗，
        // 高並發下第二筆相同 email 會在 DB unique index 拋 DbUpdateException
        // (SqlServer: SqlException 2601/2627；SQLite: SqliteException 19/2067)，
        // 不是 AppException → GlobalExceptionHandler 會轉成 500，
        // 讓 client 無法區分「真的衝突」與「系統故障」。此處將其轉譯為 409，
        // 與 ExistsByEmailAsync 成功攔截時的語意一致。
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new AppConflictException("Email is already registered.");
        }

        return user.ToDto();
    }

    // Email 以 lower-invariant + Trim 正規化，避免 DB 層大小寫敏感索引把
    // 視覺上相同的 email 當成兩筆。Trim 另防空白剪貼錯誤造成之後查無帳號。
    // 未採 NormalizedEmail 另存欄位是為了避免 schema 複雜化；downstream 若要走 AspNetCore.Identity
    // 那套「保留原 Email + 另存 NormalizedEmail」可再擴展。
    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    // 跨 DB provider 的 unique constraint 錯誤碼字串比對：
    // - SQL Server: "2601" (nonclustered)、"2627" (clustered)
    // - SQLite (Microsoft.Data.Sqlite): "UNIQUE constraint failed" / "19"
    // - PostgreSQL (Npgsql): SqlState "23505"
    // 直接比對 InnerException.Message 與型別名稱，避免對特定 provider 的 package 硬依賴。
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        var message = inner.Message ?? string.Empty;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal)
            || message.Contains("23505", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<(string AccessToken, string NewRefreshToken)> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct)
            ?? throw new AppAuthenticationException("Invalid or expired refresh token.");

        // Refresh Token Rotation 安全機制：
        // 必須嚴格區分「已撤銷（IsRevoked）」與「已過期（IsExpired）」兩種情境：
        // - IsRevoked：代表該 token 已被輪換掉（或遭強制撤銷），若仍被重用，視為竊取重放攻擊，
        //   應立即撤銷該使用者所有 session 強制重新登入，降低損害範圍。
        // - IsExpired：僅為 token 自然過期，屬正常失效路徑，只需要求使用者重新登入即可，
        //   不應連帶撤銷其他仍有效的 session（否則會在同步過期窗口誤殺所有裝置）。
        if (tokenEntity.IsRevoked)
        {
            await tokenService.RevokeAllUserRefreshTokensAsync(tokenEntity.UserId, ct);
            throw new AppAuthenticationException("Refresh token has been reused. All sessions revoked.");
        }

        if (tokenEntity.IsExpired)
        {
            throw new AppAuthenticationException("Refresh token has expired. Please log in again.");
        }

        // 先建立新 token，再撤銷舊 token，並將舊 token 的 ReplacedByToken 指向新 token
        // 的「雜湊值」（不是明文），讓稽核鏈在 DB 中也是單一格式、
        // 避免洩漏 plaintext refresh token。
        var newPair = await tokenService.CreateRefreshTokenAsync(tokenEntity.UserId, ct);
        await tokenService.RevokeRefreshTokenAsync(tokenEntity, newPair.Entity.TokenHash, ct);

        var accessToken = tokenService.GenerateAccessToken(tokenEntity.User);

        return (accessToken, newPair.RawToken);
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct);
        if (tokenEntity?.IsActive == true)
            await tokenService.RevokeRefreshTokenAsync(tokenEntity, ct: ct);
    }

}
