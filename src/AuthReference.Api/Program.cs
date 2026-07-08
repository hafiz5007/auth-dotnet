using AuthReference.Api.Endpoints;
using AuthReference.Infrastructure;
using AuthReference.Infrastructure.Authentication;
using AuthReference.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging (shorter than the Server's — no PII redactor
//    because a resource server rarely logs bodies) ─────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Lightweight infrastructure (no MediatR, no token issuer, no retention) ──
builder.Services.AddAuthReferenceValidation(builder.Configuration);
builder.Services.AddAuthReferenceJwtBearer();
builder.Services.AddAuthorization();

// ── Health checks ─────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapPublic();
app.MapProfile();
app.MapAdmin();
app.MapHealth();

await app.RunAsync();

// For WebApplicationFactory<Program> in the Api.Tests project.
public partial class Program { }
