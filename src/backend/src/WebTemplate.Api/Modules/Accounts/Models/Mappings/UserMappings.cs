using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Modules.Accounts.Models.Mappings;

/// <summary>
/// <see cref="User"/> ↔ <see cref="UserDto"/> 的單一事實來源（single source of truth）對應。
/// 任何服務層需要將 User 轉為對外 DTO 時都應呼叫此擴充方法，避免在多個類別重複宣告相同的欄位對應。
/// </summary>
public static class UserMappings
{
    public static UserDto ToDto(this User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        // Role enum 對外統一以小寫字串表示（admin/user），與前端 UserDto["role"] 型別對齊。
        Role = user.Role.ToString().ToLowerInvariant(),
        CreatedAt = user.CreatedAt,
    };
}
