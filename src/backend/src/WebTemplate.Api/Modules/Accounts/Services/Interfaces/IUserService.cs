using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;

namespace WebTemplate.Api.Modules.Accounts.Services.Interfaces;

/// <summary>使用者查詢服務介面，提供讀取使用者資料的業務邏輯。</summary>
public interface IUserService
{
    /// <summary>依 ID 查詢單一使用者。</summary>
    /// <param name="id">目標使用者的 GUID。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>找到則回傳使用者 DTO；否則回傳 <c>null</c>。</returns>
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>分頁查詢使用者列表，可選關鍵字搜尋，回傳帶分頁中繼資料的統一格式回應。</summary>
    /// <param name="page">頁碼（從 1 開始）。</param>
    /// <param name="limit">每頁筆數。</param>
    /// <param name="search">可選的搜尋關鍵字，比對 Email 與 DisplayName。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含使用者 DTO 清單與分頁中繼資料的 <see cref="ApiResponse{T}"/>。</returns>
    Task<ApiResponse<IEnumerable<UserDto>>> GetAllAsync(int page, int limit, string? search, CancellationToken ct = default);
}
