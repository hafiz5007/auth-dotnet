using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Configuration;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthReference.Infrastructure.Workers;

/// <summary>
/// Daily sweep: delete refresh tokens whose expiry is more than the retention
/// grace behind us, and audit rows older than the audit retention window.
///
/// Runs on a single node — in a multi-replica deployment you'd fence with a
/// distributed lock (Redis SET NX / Postgres advisory lock) so only one
/// replica sweeps. This impl keeps things simple and expects the deployment
/// to scale the sweep node to 1.
/// </summary>
public sealed class TokenRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly RetentionOptions _options;
    private readonly ILogger<TokenRetentionWorker> _log;

    public TokenRetentionWorker(
        IServiceScopeFactory scopes,
        IClock clock,
        IOptions<InfrastructureOptions> options,
        ILogger<TokenRetentionWorker> log)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value.Retention;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so the sweep never runs during a rolling deploy
        // window before the DB has settled.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogError(ex, "TokenRetentionWorker sweep failed; will retry after the interval");
            }

            try { await Task.Delay(_options.SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var refreshCutoff = _clock.UtcNow - _options.RefreshTokenGrace;
        var auditCutoff = _clock.UtcNow - _options.AuditRetention;

        var deletedRefresh = await db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < refreshCutoff)
            .ExecuteDeleteAsync(ct);

        var deletedAudit = await db.AuditEvents
            .Where(e => e.OccurredAtUtc < auditCutoff)
            .ExecuteDeleteAsync(ct);

        _log.LogInformation(
            "Retention sweep complete — deleted {RefreshDeleted} refresh tokens, {AuditDeleted} audit rows",
            deletedRefresh, deletedAudit);
    }
}
