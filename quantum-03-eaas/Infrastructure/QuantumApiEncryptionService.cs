using QuantumAPI.Client;
using QuantumAPI.Client.Models.Encryption;
using UsersApi.Application;

namespace UsersApi.Infrastructure;

public class QuantumApiEncryptionService : IEncryptionService
{
    private readonly QuantumAPIClient _client;

    public QuantumApiEncryptionService(QuantumAPIClient client)
    {
        _client = client;
    }

    public async Task<string> EncryptAsync(string plaintext)
    {
        var result = await _client.Encryption.EncryptAsync(new EncryptRequest
        {
            Plaintext = plaintext,
            Encoding = "utf8"
        });

        return result.EncryptedPayload;
    }

    public async Task<string> DecryptAsync(string ciphertext)
    {
        var result = await _client.Encryption.DecryptAsync(new DecryptRequest
        {
            EncryptedPayload = ciphertext
        });

        return result.Plaintext;
    }
}
