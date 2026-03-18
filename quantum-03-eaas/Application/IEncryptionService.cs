namespace UsersApi.Application;

public interface IEncryptionService
{
    Task<string> EncryptAsync(string plaintext);
    Task<string> DecryptAsync(string ciphertext);
}
