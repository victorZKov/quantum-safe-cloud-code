using System.Security.Cryptography.X509Certificates;

namespace ZeroTrust.Certificates;

/// <summary>
/// Provides ML-DSA-65 mTLS certificates for service-to-service communication.
/// Certificates are issued by the QuantumAPI CA and rotated automatically.
/// </summary>
public interface IServiceCertificateProvider
{
    /// <summary>
    /// Returns the current client certificate for outbound mTLS connections.
    /// The implementation handles rotation transparently.
    /// </summary>
    Task<X509Certificate2> GetClientCertificateAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the CA certificate used to validate peer certificates.
    /// Used by Kestrel to build the trusted certificate chain.
    /// </summary>
    Task<X509Certificate2> GetCaCertificateAsync(CancellationToken ct = default);
}
