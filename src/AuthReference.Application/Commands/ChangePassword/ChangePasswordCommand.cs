using MediatR;

namespace AuthReference.Application.Commands.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<bool>;
