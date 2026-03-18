using QuantumAPI.Client;

namespace UsersApi.Infrastructure;

public class QuantumVaultSecretProvider : ISecretProvider
{
    private readonly QuantumAPIClient _client;

    public QuantumVaultSecretProvider(QuantumAPIClient client)
    {
        _client = client;
    }

    public async Task<string> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        var secret = await _client.Secrets.GetAsync(Guid.Parse(secretId), cancellationToken);

        if (secret?.Value is null)
            throw new InvalidOperationException(
                $"Secret '{secretId}' was not found in QuantumVault or its value was null.");

        return secret.Value;
    }
}
