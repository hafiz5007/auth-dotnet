using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Responses;

namespace AuthReference.Domain.Services;

/// <summary>
/// Issues signed access tokens + opaque refresh tokens for an authenticated user.
/// Backed by OpenIddict in Infrastructure so the tokens carry the standard OIDC
/// claim set + a custom <c>tv</c> claim for the token-version revocation check.
/// </summary>
public interface ITokenIssuer
{
    Task<TokenPair> IssueAsync(
        ApplicationUser user,
        IReadOnlyList<string> scopes,
        CancellationToken ct = default);
}
