using WebTemplate.Api.Models.Entities;

namespace WebTemplate.Api.Repositories;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<(int Total, IReadOnlyList<User> Items)> FindPagedAsync(
        int page, int limit, string? search, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
