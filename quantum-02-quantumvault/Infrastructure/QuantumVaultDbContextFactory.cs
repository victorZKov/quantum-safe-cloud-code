using Microsoft.EntityFrameworkCore;

namespace UsersApi.Infrastructure;

// Called once at startup to create UsersDbContext after fetching the
// connection string from QuantumVault. Registered as the factory delegate
// for AddDbContextFactory<UsersDbContext>.
public static class QuantumVaultDbContextFactory
{
    public static async Task<string> ResolveConnectionStringAsync(
        ISecretProvider secretProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var secretId = configuration["Secrets:DbConnectionId"]
            ?? throw new InvalidOperationException(
                "Secrets:DbConnectionId is not configured. " +
                "Set it to the QuantumVault secret ID that holds your PostgreSQL connection string.");

        return await secretProvider.GetSecretAsync(secretId, cancellationToken);
    }

    public static DbContextOptions<UsersDbContext> BuildOptions(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return optionsBuilder.Options;
    }
}
