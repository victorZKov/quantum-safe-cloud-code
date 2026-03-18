using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuantumAPI.Client;
using UsersApi.Api;
using UsersApi.Application;
using UsersApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// QuantumVault
// ---------------------------------------------------------------------------
// The API key is never stored in appsettings.json.
// Set QUANTUMAPI__APIKEY as an environment variable (or a Kubernetes secret
// mounted as an env var). The double-underscore maps to "QuantumApi:ApiKey"
// in the .NET configuration hierarchy.
var quantumApiKey = builder.Configuration["QuantumApi:ApiKey"]
    ?? throw new InvalidOperationException(
        "QuantumApi:ApiKey is required. Set the QUANTUMAPI__APIKEY environment variable.");

builder.Services.AddSingleton(new QuantumAPIClient(new QuantumAPIOptions
{
    ApiKey = quantumApiKey,
    BaseUrl = "https://api.quantumapi.eu"
}));

builder.Services.AddScoped<ISecretProvider, QuantumVaultSecretProvider>();

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
// The connection string lives in QuantumVault. We fetch it once at startup
// using a temporary service scope, before the host is fully built.
//
// We register DbContext with a factory lambda so EF Core resolves the
// connection string asynchronously before the first context is created.
// The resolved string is captured as a closure -- it does not change at
// runtime, which is the correct behaviour for a connection string.
string dbConnectionString;
{
    await using var bootstrap = builder.Services.BuildServiceProvider();
    var secretProvider = bootstrap.GetRequiredService<ISecretProvider>();
    dbConnectionString = await QuantumVaultDbContextFactory.ResolveConnectionStringAsync(
        secretProvider,
        builder.Configuration);
}

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(dbConnectionString));

// ---------------------------------------------------------------------------
// JWT
// ---------------------------------------------------------------------------
// The JWT secret also comes from QuantumVault. We reuse the same bootstrap
// scope result -- the connection string fetch above already called the vault,
// so the client is already initialised.
string jwtSecret;
{
    await using var bootstrap = builder.Services.BuildServiceProvider();
    var secretProvider = bootstrap.GetRequiredService<ISecretProvider>();

    var jwtSecretId = builder.Configuration["Secrets:JwtSecretId"]
        ?? throw new InvalidOperationException(
            "Secrets:JwtSecretId is not configured. " +
            "Set it to the QuantumVault secret ID that holds your JWT signing secret.");

    jwtSecret = await secretProvider.GetSecretAsync(jwtSecretId);
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is required.");

builder.Services.AddSingleton(new JwtOptions(jwtSecret, jwtIssuer, jwtAudience));
builder.Services.AddSingleton<JwtService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Users API",
        Version = "v1",
        Description = "User management API with JWT authentication, Argon2id password hashing, " +
                      "and secrets managed by QuantumVault."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer {token}"
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

// ---------------------------------------------------------------------------
// Build and run
// ---------------------------------------------------------------------------
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
