namespace UsersApi.Domain;

// In this article, email is stored in plaintext.
// See Article 03 for the EaaS-encrypted version with EmailCiphertext + SearchableEmail.
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
