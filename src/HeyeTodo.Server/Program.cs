using System.Text;
using HeyeTodo.Server.Api.Hubs;
using HeyeTodo.Server.Application.Auth;
using HeyeTodo.Server.Application.Planning;
using HeyeTodo.Server.Application.Projects;
using HeyeTodo.Server.Application.Sync;
using HeyeTodo.Server.Application.Tasks;
using HeyeTodo.Server.Infrastructure.Auth;
using HeyeTodo.Server.Infrastructure.Localization;
using HeyeTodo.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scalar.AspNetCore;

const string DevelopmentJwtSigningKey = "HeyeTodo-Development-Signing-Key-Do-Not-Use-In-Production-2026";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

// ─── Configuration ────────────────────────────────────────────
builder.Services.Configure<ServerPlanningOptions>(builder.Configuration.GetSection("Planning:ServerProxy"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions
{
    SigningKey = "REPLACE-ME-VIA-USER-SECRETS"
};
if (JwtSigningKeyIsWeak(jwtOptions.SigningKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtOptions.SigningKey = DevelopmentJwtSigningKey;
    }
    else
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured via user-secrets or environment variables before starting in Production.");
    }
}

builder.Services.Configure<JwtOptions>(options =>
{
    builder.Configuration.GetSection("Jwt").Bind(options);
    if (JwtSigningKeyIsWeak(options.SigningKey) && builder.Environment.IsDevelopment())
    {
        options.SigningKey = DevelopmentJwtSigningKey;
    }
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// ─── EF Core / Postgres ───────────────────────────────────────
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Host=localhost;Port=0427;Database=heyetodo;Username=heyetodo;Password=heyetodo";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));

// ─── Auth ─────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // Allow JWT via query string for SignalR WebSocket negotiation.
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/ws"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ─── DI ───────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddHttpClient<IPlanningLlmDriver, ServerProxyLlmDriver>();
builder.Services.AddScoped<IPlanningService, PlanningService>();
builder.Services.AddScoped<ISyncService, SyncService>();

// ─── Web layer ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHeyeLocalization();

// SignalR + MessagePack
builder.Services.AddSignalR().AddMessagePackProtocol();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (jwtOptions.SigningKey == DevelopmentJwtSigningKey)
{
    app.Logger.LogWarning("Jwt:SigningKey is not configured securely. A development-only fallback key is being used. Use 'dotnet user-secrets' for a stable local key.");
}

if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning("CORS has no allowed origins configured. All browser cross-origin requests will be rejected.");
}

// Apply pending EF Core migrations at startup so self-hosted instances come up with a
// matching schema without a separate deploy step. This is acceptable for MVP / small-team use.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (NpgsqlException exception)
    {
        app.Logger.LogCritical(
            exception,
            "PostgreSQL is not reachable. For local development, start it with 'docker compose -f deploy/docker-compose.yml up -d postgres' and verify localhost:0427 is accepting connections.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("HeyeTodo API");
        options.WithTheme(ScalarTheme.BluePlanet);
    });
}

app.UseHeyeLocalization();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SyncHub>("/ws/sync");

app.MapGet("/", () => Results.Ok(new { service = "HeyeTodo.Server", status = "ok" }));

app.Run();

static bool JwtSigningKeyIsWeak(string? signingKey)
    => string.IsNullOrWhiteSpace(signingKey)
       || signingKey.StartsWith("REPLACE-ME", StringComparison.Ordinal)
       || Encoding.UTF8.GetByteCount(signingKey) < 32;
