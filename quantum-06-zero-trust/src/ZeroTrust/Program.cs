using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using ZeroTrust.Certificates;
using ZeroTrust.Http;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// QuantumAPI certificate provider
// ─────────────────────────────────────────────────────────────
builder.Services.Configure<QuantumApiOptions>(
    builder.Configuration.GetSection("QuantumApi"));

// HttpClient used by the certificate provider itself (plain HTTPS, no mTLS)
builder.Services.AddHttpClient<IServiceCertificateProvider, QuantumApiCertificateProvider>(client =>
{
    client.DefaultRequestHeaders.Add(
        "Authorization",
        $"Bearer {builder.Configuration["QuantumApi:ApiKey"]}");
});

// Background service that refreshes certificates before expiry
builder.Services.AddHostedService<CertificateRotationService>();

// ─────────────────────────────────────────────────────────────
// Kestrel: require mTLS on all connections
// ─────────────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    var certProvider = serverOptions.ApplicationServices
        .GetRequiredService<IServiceCertificateProvider>();

    serverOptions.ConfigureHttpsDefaults(https =>
    {
        // Require client certificate — no anonymous connections
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

        // Validate against QuantumAPI CA
        https.ClientCertificateValidation = (cert, chain, errors) =>
        {
            var caCert = certProvider.GetCaCertificateAsync().GetAwaiter().GetResult();

            using var customChain = new X509Chain();
            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            customChain.ChainPolicy.CustomTrustStore.Add(caCert);
            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            return customChain.Build(cert);
        };
    });
});

// ─────────────────────────────────────────────────────────────
// Certificate-based authentication
// ─────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        // Only accept certificates from the QuantumAPI CA
        options.AllowedCertificateTypes = CertificateTypes.Chained;
        options.RevocationMode = X509RevocationMode.NoCheck;

        // Map certificate CN to ClaimsPrincipal
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                var cn = context.ClientCertificate.GetNameInfo(X509NameType.SimpleName, false);
                var claims = new[]
                {
                    new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.Name, cn),
                    new System.Security.Claims.Claim("service", cn)
                };

                context.Principal = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────────────────────
// Service-to-service mTLS clients
// ─────────────────────────────────────────────────────────────

// Client for calling the Orders service with mTLS
builder.Services.AddMtlsHttpClient("orders-service", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrdersUrl"]
        ?? throw new InvalidOperationException("Services:OrdersUrl not configured"));
});

// Client for calling the Notifications service with mTLS
builder.Services.AddMtlsHttpClient("notifications-service", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationsUrl"]
        ?? throw new InvalidOperationException("Services:NotificationsUrl not configured"));
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
