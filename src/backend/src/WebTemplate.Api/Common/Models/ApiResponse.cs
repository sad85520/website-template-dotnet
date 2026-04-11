namespace WebTemplate.Api.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public IEnumerable<FieldError>? Errors { get; set; }
    public PaginationMeta? Meta { get; set; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Created(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string message, IEnumerable<FieldError>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static ApiResponse<T> Paginated(T data, PaginationMeta meta) =>
        new() { Success = true, Data = data, Meta = meta };
}

public class FieldError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PaginationMeta
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}
