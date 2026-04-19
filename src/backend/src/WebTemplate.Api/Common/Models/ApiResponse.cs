namespace WebTemplate.Api.Common.Models;

/// <summary>統一 API 回應包裝器。以 <c>sealed record</c> + <c>init</c> 設計避免呼叫端回傳後意外改寫欄位。</summary>
/// <typeparam name="T">資料酬載的型別。</typeparam>
public sealed record ApiResponse<T>
{
    /// <summary>請求是否成功。</summary>
    public required bool Success { get; init; }

    /// <summary>回應資料；失敗時為 <c>null</c>。</summary>
    public T? Data { get; init; }

    /// <summary>錯誤或提示訊息；成功時通常為 <c>null</c>。</summary>
    public string? Message { get; init; }

    /// <summary>欄位層級驗證錯誤清單；僅驗證失敗時填入。</summary>
    public IEnumerable<FieldError>? Errors { get; init; }

    /// <summary>分頁中繼資料；僅分頁查詢回應時填入。</summary>
    public PaginationMeta? Meta { get; init; }

    /// <summary>建立代表查詢成功的回應。</summary>
    /// <param name="data">要回傳的資料。</param>
    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    /// <summary>建立代表資源建立成功（HTTP 201）的回應。</summary>
    /// <param name="data">已建立的資源。</param>
    public static ApiResponse<T> Created(T data) =>
        new() { Success = true, Data = data };

    /// <summary>建立代表操作失敗的回應。</summary>
    /// <param name="message">描述失敗原因的訊息。</param>
    /// <param name="errors">可選的欄位層級錯誤清單。</param>
    public static ApiResponse<T> Fail(string message, IEnumerable<FieldError>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    /// <summary>建立帶有分頁中繼資料的成功回應。</summary>
    /// <param name="data">當前頁的資料集合。</param>
    /// <param name="meta">分頁中繼資料。</param>
    public static ApiResponse<T> Paginated(T data, PaginationMeta meta) =>
        new() { Success = true, Data = data, Meta = meta };
}

/// <summary>
/// 非泛型 <see cref="ApiResponse{T}"/> 的無資料成功回應工廠，避免呼叫端使用
/// <c>ApiResponse&lt;object&gt;.Ok(null!)</c> 這種 null-forgiving 黑魔法產生無資料回應。
/// </summary>
public static class ApiResponse
{
    /// <summary>建立不帶資料的成功回應（例如登出、刪除等不需回傳 payload 的操作）。</summary>
    /// <returns>Data = <c>null</c> 的成功 <see cref="ApiResponse{T}"/>。</returns>
    public static ApiResponse<object?> Ok() =>
        new() { Success = true };
}

/// <summary>單一欄位的驗證錯誤。</summary>
public sealed record FieldError
{
    /// <summary>發生錯誤的欄位名稱。</summary>
    public required string Field { get; init; }

    /// <summary>欄位的錯誤訊息。</summary>
    public required string Message { get; init; }
}

/// <summary>分頁查詢的中繼資料。</summary>
public sealed record PaginationMeta
{
    /// <summary>符合條件的資料總筆數（不含分頁限制）。</summary>
    public required int Total { get; init; }

    /// <summary>目前頁碼（從 1 開始）。</summary>
    public required int Page { get; init; }

    /// <summary>每頁筆數。</summary>
    public required int Limit { get; init; }

    /// <summary>總頁數，由 <see cref="Total"/> 和 <see cref="Limit"/> 計算而得。</summary>
    public int TotalPages => Limit > 0 ? (int)Math.Ceiling((double)Total / Limit) : 0;
}
