using AuthReference.Domain.Models.Enums;

namespace AuthReference.Domain.Models.Responses;

/// <summary>
/// Login outcome. When <see cref="Decision"/> is <see cref="AuthDecision.Granted"/>,
/// <see cref="Tokens"/> is non-null; otherwise it's null and the decision is the
/// only meaningful field.
/// </summary>
public record LoginResponse(AuthDecision Decision, TokenPair? Tokens)
{
    public static LoginResponse Granted(TokenPair tokens) => new(AuthDecision.Granted, tokens);
    public static LoginResponse Denied(AuthDecision reason) => new(reason, null);
}
