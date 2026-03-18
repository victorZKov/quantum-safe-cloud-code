using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ZeroTrust.Certificates;

/// <summary>
/// Fetches ML-DSA-65 mTLS certificates from the QuantumAPI CA.
/// Caches the active certificate and refreshes it before expiry.
/// </summary>
public sealed class QuantumApiCertificateProvider : IServiceCertificateProvider
{
    private readonly HttpClient _http;
    private readonly QuantumApiOptions _options;
    private readonly ILogger<QuantumApiCertificateProvider> _logger;
    private readonly object _certLock = new();

    private X509Certificate2? _clientCert;
    private X509Certificate2? _caCert;
    private DateTimeOffset _clientCertExpiry = DateTimeOffset.MinValue;

    // Renew when less than 20% of lifetime remains
    private const double RenewThreshold = 0.8;

    public QuantumApiCertificateProvider(
        HttpClient http,
        IOptions<QuantumApiOptions> options,
        ILogger<QuantumApiCertificateProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<X509Certificate2> GetClientCertificateAsync(CancellationToken ct = default)
    {
        lock (_certLock)
        {
            if (_clientCert is not null && DateTimeOffset.UtcNow < _clientCertExpiry)
                return _clientCert;
        }

        _logger.LogInformation("Requesting new mTLS client certificate from QuantumAPI CA");

        // POST /v1/pki/issue — request a new ML-DSA-65 certificate
        var request = new
        {
            common_name = _options.ServiceName,
            ttl = "24h",
            key_type = "ml-dsa-65"
        };

        using var response = await _http.PostAsJsonAsync(
            $"{_options.BaseUrl}/v1/pki/issue",
            request,
            ct);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<IssueCertificateResponse>(
            cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from QuantumAPI CA");

        // Parse PEM-encoded certificate + private key
        var certPem = body.Data.CertificatePem;
        var keyPem = body.Data.PrivateKeyPem;
        var cert = X509Certificate2.CreateFromPem(certPem, keyPem);

        // Cache until 80% of lifetime has elapsed
        var notAfter = cert.NotAfter;
        var notBefore = cert.NotBefore;
        var lifetime = notAfter - notBefore;
        var expiry = notBefore + TimeSpan.FromTicks((long)(lifetime.Ticks * RenewThreshold));

        lock (_certLock)
        {
            _clientCert = cert;
            _clientCertExpiry = expiry;
        }

        _logger.LogInformation(
            "New mTLS certificate issued. Valid until {NotAfter}, renewing after {Threshold}",
            notAfter,
            expiry);

        return cert;
    }

    public async Task<X509Certificate2> GetCaCertificateAsync(CancellationToken ct = default)
    {
        if (_caCert is not null)
            return _caCert;

        using var response = await _http.GetAsync(
            $"{_options.BaseUrl}/v1/pki/ca/pem",
            ct);

        response.EnsureSuccessStatusCode();

        var pem = await response.Content.ReadAsStringAsync(ct);
        _caCert = X509Certificate2.CreateFromPem(pem);

        return _caCert;
    }

    // Response DTOs
    private sealed record IssueCertificateResponse(CertificateData Data);
    private sealed record CertificateData(
        string CertificatePem,
        string PrivateKeyPem,
        DateTimeOffset Expiration);
}

public sealed class QuantumApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
}
