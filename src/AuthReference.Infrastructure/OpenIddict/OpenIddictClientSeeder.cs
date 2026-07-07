using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthReference.Infrastructure.OpenIddict;

/// <summary>
/// Idempotent client seed: on startup, ensures the two demo OAuth applications
/// exist. Real production would provision clients via an admin API and not
/// need a seeder — this exists because a fresh checkout should just work.
///
/// Called from <see cref="DependencyInjection"/> during host startup.
/// </summary>
public sealed class OpenIddictClientSeeder
{
    private const string ApiScope = "api";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<OpenIddictClientSeeder>>();
        var apps = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<OpenIddictClientOptions>>().Value;

        // --- Scope ---
        if (await scopes.FindByNameAsync(ApiScope, ct) is null)
        {
            await scopes.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = ApiScope,
                Resources = { ApiScope }
            }, ct);
            log.LogInformation("Seeded scope: {Scope}", ApiScope);
        }

        // --- SPA (public client, auth-code + PKCE, no secret) ---
        if (await apps.FindByClientIdAsync(opts.Spa.ClientId, ct) is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = opts.Spa.ClientId,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                DisplayName = opts.Spa.DisplayName,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope + ApiScope
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            };
            foreach (var uri in opts.Spa.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
            foreach (var uri in opts.Spa.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

            await apps.CreateAsync(descriptor, ct);
            log.LogInformation("Seeded SPA client: {ClientId}", opts.Spa.ClientId);
        }

        // --- Backend worker (confidential client, client-credentials) ---
        if (await apps.FindByClientIdAsync(opts.Worker.ClientId, ct) is null)
        {
            await apps.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = opts.Worker.ClientId,
                ClientSecret = opts.Worker.ClientSecret,
                ClientType = ClientTypes.Confidential,
                DisplayName = opts.Worker.DisplayName,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + ApiScope
                }
            }, ct);
            log.LogInformation("Seeded worker client: {ClientId}", opts.Worker.ClientId);
        }
    }
}
