using AuthReference.Application;
using AuthReference.Application.Abstractions;
using AuthReference.Infrastructure;
using AuthReference.Infrastructure.Persistence;
using AuthReference.Server.Auth;
using AuthReference.Server.Endpoints;
using AuthReference.Server.HealthChecks;
using AuthReference.Server.Logging;
using AuthReference.Server.Middleware;
using AuthReference.Server.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging ────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<PiiRedactor>()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// ── Application + Infrastructure ─────────────────────────────
builder.Services.AddAuthReferenceApplication();
builder.Services.AddAuthReferenceInfrastructure(builder.Configuration);

// ── HttpContext-backed IRequestContext overrides the Headless fallback ────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpContextRequestContext>();

// ── JWT bearer + authorization ────────────────────────────────
builder.Services.AddAuthReferenceJwtBearer();
builder.Services.AddAuthorization();

// ── Rate limiting ─────────────────────────────────────────────
builder.Services.AddRateLimiter(AuthRateLimitPolicies.Configure);

// ── Health checks — Postgres always, Redis when configured ────
var healthBuilder = builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });

var redisConn = builder.Configuration["AuthReference:Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    healthBuilder.AddCheck<RedisHealthCheck>(
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "cache" });
}

// ── OpenAPI ───────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Startup: seed OpenIddict demo clients ────────────────────
await app.Services.SeedOpenIddictClientsAsync();

// ── Pipeline ─────────────────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ValidationExceptionMiddleware>();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────
app.MapLogin();
app.MapRegister();
app.MapRefresh();
app.MapChangePassword();
app.MapRevokeAll();
app.MapHealth();

await app.RunAsync();

// Expose Program for WebApplicationFactory<Program> in the Phase 5 tests.
public partial class Program { }
