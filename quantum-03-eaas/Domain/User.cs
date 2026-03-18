namespace UsersApi.Domain;

public class User
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Encrypted email ciphertext produced by QuantumAPI EaaS.
    /// Never store plaintext email in this column.
    /// </summary>
    public string EmailCiphertext { get; set; } = string.Empty;

    /// <summary>
    /// SHA3-256 hash of the lowercased, trimmed email address.
    /// Used for login lookups without decrypting the ciphertext.
    /// </summary>
    public string SearchableEmail { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted phone number ciphertext. Null when the user has no phone on record.
    /// </summary>
    public string? PhoneNumberCiphertext { get; set; }

    /// <summary>
    /// Argon2id password hash in the format {base64(salt)}${base64(hash)}.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
