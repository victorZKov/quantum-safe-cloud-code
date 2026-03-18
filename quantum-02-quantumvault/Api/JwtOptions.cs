namespace UsersApi.Api;

// Holds the JWT signing secret and token parameters resolved at startup.
// Registered as a singleton so JwtService and the bearer middleware share
// the same values without hitting configuration again.
public sealed record JwtOptions(string Secret, string Issuer, string Audience);
