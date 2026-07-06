using MediatR;

namespace AuthReference.Application.Commands.RevokeAll;

/// <summary>
/// Admin-scope. Kills every outstanding session for the target user.
/// Callers must present a JWT with an admin role; policy enforcement lives
/// in the API layer.
/// </summary>
public record RevokeAllCommand(Guid TargetUserId, string Reason, Guid? InvokingUserId) : IRequest<Unit>;
