using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UsersApi.Application;
using UsersApi.Domain;

namespace UsersApi.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _db;

    private const int Argon2Parallelism = 1;
    private const int Argon2MemorySize = 65536; // 64 MB
    private const int Argon2Iterations = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public UserRepository(UsersDbContext db)
    {
        _db = db;
    }

    public async Task<User> CreateAsync(User user, string plainPassword, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        user.OwnerId = user.Id;
        user.CreatedAt = DateTimeOffset.UtcNow;
        user.IsDeleted = false;
        user.PasswordHash = HashPassword(plainPassword);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FirstOrDefaultAsync(
            u => u.Email == email.ToLowerInvariant(),
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Users.AnyAsync(
            u => u.Email == email.ToLowerInvariant(),
            cancellationToken);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _db.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.IsDeleted, true),
                cancellationToken);
    }

    public bool VerifyPassword(string plainPassword, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var computedHash = ComputeHash(plainPassword, salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static string HashPassword(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(plainPassword, salt);
        return $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static byte[] ComputeHash(string plainPassword, byte[] salt)
    {
        var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt = salt,
            DegreeOfParallelism = Argon2Parallelism,
            MemorySize = Argon2MemorySize,
            Iterations = Argon2Iterations
        };

        return argon2.GetBytes(HashSize);
    }
}
