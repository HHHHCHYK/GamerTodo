using System.Text;
using HeyeTodo.Server.Api.Hubs;
using HeyeTodo.Server.Application.Auth;
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
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

// ─── Configuration ────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions
{
    SigningKey = "REPLACE-ME-VIA-USER-SECRETS"
};

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// ─── EF Core / Postgres ───────────────────────────────────────
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Host=localhost;Port=55432;Database=heyetodo;Username=heyetodo;Password=heyetodo";
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

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    || jwtOptions.SigningKey.StartsWith("REPLACE-ME", StringComparison.Ordinal)
    || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    if (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning("Jwt:SigningKey is not configured securely. Use 'dotnet user-secrets' during development.");
    }
    else
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured via user-secrets or environment variables before starting in Production.");
    }
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
    db.Database.Migrate();
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
