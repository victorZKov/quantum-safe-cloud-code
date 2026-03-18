using Microsoft.EntityFrameworkCore;
using QuantumAPI.Client;
using UsersApi.Application;
using UsersApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ─── QuantumAPI client ───────────────────────────────────────────────────────
var quantumApiKey = builder.Configuration["QuantumApi:ApiKey"]
    ?? Environment.GetEnvironmentVariable("QAPI_API_KEY")
    ?? throw new InvalidOperationException(
        "QuantumApi:ApiKey or QAPI_API_KEY environment variable is required.");

builder.Services.AddSingleton(new QuantumAPIClient(new QuantumAPIOptions
{
    ApiKey = quantumApiKey,
    BaseUrl = builder.Configuration["QuantumApi:BaseUrl"] ?? "https://api.quantumapi.eu"
}));

// ─── EaaS encryption service ─────────────────────────────────────────────────
builder.Services.AddScoped<IEncryptionService, QuantumApiEncryptionService>();

// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.")));

// ─── Application services ────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();
await app.RunAsync();
