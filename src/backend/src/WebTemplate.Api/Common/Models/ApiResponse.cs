namespace WebTemplate.Api.Common.Models;

/// <summary>統一 API 回應包裝器。</summary>
/// <typeparam name="T">資料酬載的型別。</typeparam>
public class ApiResponse<T>
{
    /// <summary>請求是否成功。</summary>
    public bool Success { get; set; }

    /// <summary>回應資料；失敗時為 <c>null</c>。</summary>
    public T? Data { get; set; }

    /// <summary>錯誤或提示訊息；成功時通常為 <c>null</c>。</summary>
    public string? Message { get; set; }

    /// <summary>欄位層級驗證錯誤清單；僅驗證失敗時填入。</summary>
    public IEnumerable<FieldError>? Errors { get; set; }

    /// <summary>分頁中繼資料；僅分頁查詢回應時填入。</summary>
    public PaginationMeta? Meta { get; set; }

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

/// <summary>單一欄位的驗證錯誤。</summary>
public class FieldError
{
    /// <summary>發生錯誤的欄位名稱。</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>欄位的錯誤訊息。</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>分頁查詢的中繼資料。</summary>
public class PaginationMeta
{
    /// <summary>符合條件的資料總筆數（不含分頁限制）。</summary>
    public int Total { get; set; }

    /// <summary>目前頁碼（從 1 開始）。</summary>
    public int Page { get; set; }

    /// <summary>每頁筆數。</summary>
    public int Limit { get; set; }

    /// <summary>總頁數，由 <see cref="Total"/> 和 <see cref="Limit"/> 計算而得。</summary>
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}
