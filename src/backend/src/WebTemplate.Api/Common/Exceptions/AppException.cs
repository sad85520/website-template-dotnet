namespace WebTemplate.Api.Common.Exceptions;

/// <summary>
/// 應用程式層的業務例外基底類別。
/// 只有此類別（與其子類別）的 <see cref="Exception.Message"/> 會被
/// <c>GlobalExceptionHandler</c> 原樣回傳給客戶端；其他任何未預期例外都會被
/// 轉為通用的 500 錯誤訊息，避免意外洩漏內部細節（stack trace、SQL、路徑等）。
/// 服務層在拋出可預期的業務錯誤時應建立此類型的子類別，
/// 確保訊息內容已經過人工審核、可安全對外揭露。
/// </summary>
public abstract class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>帳號或密碼驗證失敗、session 被撤銷等登入相關錯誤（HTTP 401）。</summary>
public sealed class AppAuthenticationException(string message)
    : AppException(StatusCodes.Status401Unauthorized, message);

/// <summary>違反業務規則（例如 Email 已註冊）造成的衝突（HTTP 409）。</summary>
public sealed class AppConflictException(string message)
    : AppException(StatusCodes.Status409Conflict, message);

/// <summary>請求的資源不存在（HTTP 404）。</summary>
public sealed class AppNotFoundException(string message)
    : AppException(StatusCodes.Status404NotFound, message);

/// <summary>用戶輸入不符合規則（HTTP 400）。</summary>
public sealed class AppValidationException(string message)
    : AppException(StatusCodes.Status400BadRequest, message);
