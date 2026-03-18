using System.Text.Json;

namespace UsersApi.Api;

/// <summary>
/// Checks with QuantumID whether a token is still active (not revoked).
/// Call this in endpoints that need real-time revocation awareness, for
/// example after a user deletes their account or revokes all sessions.
/// </summary>
public interface ITokenIntrospectionService
{
    /// <summary>
    /// Returns true if the token is active according to QuantumID's
    /// introspection endpoint. Returns false on any error or inactive state.
    /// </summary>
    Task<bool> IsActiveAsync(string token, CancellationToken cancellationToken = default);
}

public class TokenIntrospectionService : ITokenIntrospectionService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TokenIntrospectionService> _logger;

    public TokenIntrospectionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<TokenIntrospectionService> logger)
    {
        _http = httpClientFactory.CreateClient("introspection");
        _config = config;
        _logger = logger;
    }

    public async Task<bool> IsActiveAsync(string token, CancellationToken cancellationToken = default)
    {
        var clientId = _config["Oidc:ClientId"]
            ?? throw new InvalidOperationException("Oidc:ClientId is required.");
        var clientSecret = _config["Oidc:ClientSecret"]
            ?? throw new InvalidOperationException("Oidc:ClientSecret is required. Set it via OIDC__CLIENTSECRET environment variable.");

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", token),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/connect/introspect", body, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail closed: if QuantumID is unreachable, treat the token as inactive
            _logger.LogError(ex, "Token introspection request to QuantumID failed.");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Token introspection returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            return false;
        }

        var json = await JsonSerializer.DeserializeAsync<IntrospectionResponse>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return json?.Active ?? false;
    }

    private sealed class IntrospectionResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("active")]
        public bool Active { get; init; }
    }
}
