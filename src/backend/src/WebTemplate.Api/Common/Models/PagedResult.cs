namespace WebTemplate.Api.Common.Models;

/// <summary>
/// 純分頁資料結果（不含 HTTP envelope），用於 service 層回傳分頁查詢結果。
/// Controller 負責將此結果包進 <see cref="ApiResponse{T}"/>，避免服務層洩漏 HTTP 語意。
/// </summary>
/// <typeparam name="T">資料項目型別。</typeparam>
/// <param name="Items">當前頁的資料集合。</param>
/// <param name="Total">符合條件的資料總筆數（不含分頁限制）。</param>
/// <param name="Page">目前頁碼（從 1 開始）。</param>
/// <param name="Limit">每頁筆數。</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int Limit);
