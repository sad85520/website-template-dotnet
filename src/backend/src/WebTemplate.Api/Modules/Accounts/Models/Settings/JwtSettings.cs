namespace WebTemplate.Api.Modules.Accounts.Models.Settings;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "WebTemplate";
    public string Audience { get; set; } = "WebTemplate";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
