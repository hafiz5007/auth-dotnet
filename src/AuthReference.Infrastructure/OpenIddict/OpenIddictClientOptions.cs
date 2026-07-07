namespace AuthReference.Infrastructure.OpenIddict;

/// <summary>
/// Bound to the "AuthReference:OpenIddict" configuration section. Lets
/// the seeded clients be tuned per environment without a code change —
/// production would move the confidential-client secret into Key Vault.
/// </summary>
public class OpenIddictClientOptions
{
    public const string SectionName = "AuthReference:OpenIddict";

    public SpaClientOptions Spa { get; set; } = new();
    public WorkerClientOptions Worker { get; set; } = new();

    public class SpaClientOptions
    {
        public string ClientId { get; set; } = "spa-client";
        public string DisplayName { get; set; } = "SPA Client (Auth Code + PKCE)";
        public List<string> RedirectUris { get; set; } = new() { "http://localhost:3000/callback" };
        public List<string> PostLogoutRedirectUris { get; set; } = new() { "http://localhost:3000/" };
    }

    public class WorkerClientOptions
    {
        public string ClientId { get; set; } = "backend-worker";
        public string ClientSecret { get; set; } = "worker-secret-replace-in-prod";
        public string DisplayName { get; set; } = "Backend Worker (Client Credentials)";
    }
}
