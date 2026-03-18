using Microsoft.Extensions.Options;

namespace ZeroTrust.Certificates;

/// <summary>
/// Background service that proactively refreshes the mTLS client certificate
/// before it expires. Runs every 6 hours and forces renewal when the certificate
/// is within 20% of its total lifetime.
/// </summary>
public sealed class CertificateRotationService : BackgroundService
{
    private readonly IServiceCertificateProvider _certProvider;
    private readonly ILogger<CertificateRotationService> _logger;

    // Check interval — much shorter than cert lifetime (24h), so renewal is proactive
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);
    private const int MaxConsecutiveFailures = 10;

    public CertificateRotationService(
        IServiceCertificateProvider certProvider,
        ILogger<CertificateRotationService> logger)
    {
        _certProvider = certProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certificate rotation service started");

        // Initial fetch on startup — fail fast if CA is unreachable
        await _certProvider.GetClientCertificateAsync(stoppingToken);
        await _certProvider.GetCaCertificateAsync(stoppingToken);

        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            try
            {
                // GetClientCertificateAsync checks expiry threshold internally
                // and only calls the CA if renewal is needed
                await _certProvider.GetClientCertificateAsync(stoppingToken);
                consecutiveFailures = 0;
                _logger.LogDebug("Certificate rotation check completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consecutiveFailures++;
                var backoff = TimeSpan.FromTicks(
                    Math.Min(
                        InitialBackoff.Ticks * (1L << Math.Min(consecutiveFailures - 1, 20)),
                        MaxBackoff.Ticks));

                _logger.LogError(
                    ex,
                    "Certificate rotation failed ({Failures} consecutive). Retrying in {Backoff}",
                    consecutiveFailures,
                    backoff);

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogCritical(
                        "Certificate rotation has failed {Failures} times in a row. "
                        + "Current certificate may expire before renewal succeeds",
                        consecutiveFailures);
                }

                await Task.Delay(backoff, stoppingToken);
            }
        }

        _logger.LogInformation("Certificate rotation service stopped");
    }
}
