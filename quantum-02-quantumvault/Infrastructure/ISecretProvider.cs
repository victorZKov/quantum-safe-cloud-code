namespace UsersApi.Infrastructure;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string secretId, CancellationToken cancellationToken = default);
}
