using Microsoft.EntityFrameworkCore;
using UsersApi.Application;
using UsersApi.Domain;

namespace UsersApi.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _db;

    public UserRepository(UsersDbContext db)
    {
        _db = db;
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTimeOffset.UtcNow;
        user.IsDeleted = false;

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
        => await _db.Users.AnyAsync(
            u => u.Email == email.Trim().ToLowerInvariant(),
            cancellationToken);

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.IsDeleted, true),
                cancellationToken);
}
