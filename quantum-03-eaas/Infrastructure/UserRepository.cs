using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UsersApi.Application;
using UsersApi.Domain;

namespace UsersApi.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _db;
    private readonly IEncryptionService _encryption;

    private const int Argon2Parallelism = 1;
    private const int Argon2MemorySize = 65536; // 64 MB
    private const int Argon2Iterations = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public UserRepository(UsersDbContext db, IEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<User> CreateAsync(User user, string plainPassword, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        user.OwnerId = user.Id;
        user.CreatedAt = DateTimeOffset.UtcNow;
        user.IsDeleted = false;
        user.PasswordHash = HashPassword(plainPassword);

        // Capture plaintext values before encryption overwrites them
        var plaintextEmail = user.EmailCiphertext;
        var plaintextPhone = user.PhoneNumberCiphertext;

        // Encrypt email and phone in parallel — both are independent EaaS calls
        var emailTask = _encryption.EncryptAsync(plaintextEmail);
        var phoneTask = plaintextPhone is not null
            ? _encryption.EncryptAsync(plaintextPhone)
            : null;

        if (phoneTask is not null)
            await Task.WhenAll(emailTask, phoneTask);
        else
            await emailTask;

        // SearchableEmail lets us find the user at login time without decrypting
        user.SearchableEmail = HashEmail(plaintextEmail);
        user.EmailCiphertext = emailTask.Result;
        user.PhoneNumberCiphertext = phoneTask?.Result;

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return null;

        await DecryptPiiAsync(user);
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var searchHash = HashEmail(email);

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.SearchableEmail == searchHash,
            cancellationToken);

        if (user is null)
            return null;

        await DecryptPiiAsync(user);
        return user;
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var searchHash = HashEmail(email);
        return await _db.Users.AnyAsync(u => u.SearchableEmail == searchHash, cancellationToken);
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

    // Decrypt email and phone in parallel after a DB fetch.
    private async Task DecryptPiiAsync(User user)
    {
        var emailTask = _encryption.DecryptAsync(user.EmailCiphertext);
        var phoneTask = user.PhoneNumberCiphertext is not null
            ? _encryption.DecryptAsync(user.PhoneNumberCiphertext)
            : null;

        if (phoneTask is not null)
            await Task.WhenAll(emailTask, phoneTask);
        else
            await emailTask;

        user.EmailCiphertext = emailTask.Result;
        user.PhoneNumberCiphertext = phoneTask?.Result;
    }

    // SHA3-256 of the lowercased, trimmed email. Used as the searchable index value.
    private static string HashEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var bytes = SHA3_256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(bytes); // 64 hex chars
    }

    private static string HashPassword(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(plainPassword, salt);
        return $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static byte[] ComputeHash(string plainPassword, byte[] salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt = salt,
            DegreeOfParallelism = Argon2Parallelism,
            MemorySize = Argon2MemorySize,
            Iterations = Argon2Iterations
        };

        return argon2.GetBytes(HashSize);
    }
}
