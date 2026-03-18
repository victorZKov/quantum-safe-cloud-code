using UsersApi.Domain;

namespace UsersApi.Application;

public interface IUserRepository
{
    Task<User> CreateAsync(User user, string plainPassword, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    bool VerifyPassword(string plainPassword, string storedHash);
}
