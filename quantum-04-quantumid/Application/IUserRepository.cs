using UsersApi.Domain;

namespace UsersApi.Application;

// Simplified interface for Article 04: no password parameter since QuantumID
// owns authentication. User provisioning creates a record without a local password.
public interface IUserRepository
{
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
