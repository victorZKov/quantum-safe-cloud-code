namespace ZeroTrust.Http;

using ZeroTrust.Certificates;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Creates HttpClient instances configured for mTLS:
/// - Client certificate from QuantumAPI CA (ML-DSA-65)
/// - Server certificate validated against the QuantumAPI CA
/// - No fallback to standard TLS — mTLS is required for all inter-service calls
/// </summary>
public static class MtlsHttpClientExtensions
{
    /// <summary>
    /// Registers a named HttpClient configured for mTLS using QuantumAPI CA certificates.
    /// The client certificate is fetched lazily on first request and cached until renewal.
    /// </summary>
    public static IHttpClientBuilder AddMtlsHttpClient(
        this IServiceCollection services,
        string clientName,
        Action<HttpClient>? configure = null)
    {
        return services
            .AddHttpClient(clientName, configure ?? (_ => { }))
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var certProvider = serviceProvider.GetRequiredService<IServiceCertificateProvider>();
                var logger = serviceProvider.GetRequiredService<ILogger<SocketsHttpHandler>>();

                var handler = new SocketsHttpHandler
                {
                    // Keep connections alive — certificates are stable for 24h
                    PooledConnectionLifetime = TimeSpan.FromHours(1),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        // Provide the ML-DSA-65 client certificate
                        LocalCertificateSelectionCallback = (_, _, _, _, _) =>
                        {
                            // GetClientCertificateAsync is async but LocalCertificateSelectionCallback
                            // is sync. Use GetAwaiter().GetResult() here — this runs rarely
                            // (first connection + after rotation) and doesn't block the thread pool.
                            return certProvider.GetClientCertificateAsync().GetAwaiter().GetResult();
                        },

                        // Validate server certificate against QuantumAPI CA
                        RemoteCertificateValidationCallback = (_, serverCert, chain, errors) =>
                        {
                            if (serverCert is null)
                            {
                                logger.LogWarning("Server presented no certificate — rejecting connection");
                                return false;
                            }

                            // Build a chain using only the QuantumAPI CA
                            var caCert = certProvider.GetCaCertificateAsync().GetAwaiter().GetResult();

                            using var customChain = new X509Chain();
                            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                            customChain.ChainPolicy.CustomTrustStore.Add(caCert);
                            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                            var valid = customChain.Build(new X509Certificate2(serverCert));
                            if (!valid)
                            {
                                logger.LogWarning(
                                    "Server certificate validation failed: {Errors}",
                                    string.Join(", ", customChain.ChainStatus.Select(s => s.StatusInformation)));
                            }

                            return valid;
                        }
                    }
                };

                return handler;
            });
    }
}
