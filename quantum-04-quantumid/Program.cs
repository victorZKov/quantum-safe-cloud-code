using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UsersApi.Api;
using UsersApi.Application;
using UsersApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();

// --- Token introspection (revocation check) ---
builder.Services.AddHttpClient("introspection", client =>
{
    client.BaseAddress = new Uri("https://id.quantumapi.eu");
    client.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15)
});

builder.Services.AddScoped<ITokenIntrospectionService, TokenIntrospectionService>();

// --- Authentication: delegate to QuantumID as OIDC provider ---
var oidcAuthority = builder.Configuration["Oidc:Authority"]
    ?? throw new InvalidOperationException("Oidc:Authority is required.");
var oidcAudience = builder.Configuration["Oidc:Audience"]
    ?? throw new InvalidOperationException("Oidc:Audience is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // QuantumID discovery document: https://id.quantumapi.eu/.well-known/openid-configuration
        options.Authority = oidcAuthority;
        options.Audience = oidcAudience;

        // Never skip HTTPS in production — QuantumID requires it
        options.RequireHttpsMetadata = true;

        // Reuse the same connection pool for JWKS fetches instead of creating
        // a new HttpClient on every key rotation check
        options.BackchannelHttpHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };

        // Automatically refresh the signing keys when a token arrives that
        // was signed with a key not yet in the local cache
        options.RefreshOnIssuerKeyNotFound = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,

            // 30 seconds of clock skew handles minor drift between services;
            // do not use TimeSpan.Zero in distributed environments
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- MVC + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Users API",
        Version = "v1",
        Description = "User management API. Authentication handled by QuantumID (OIDC)."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token issued by QuantumID. Example: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<UsersDbContext>("database");

// --- Pipeline ---
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Users API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
