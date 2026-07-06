using FluentValidation;

namespace AuthReference.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(200);
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must differ from current password.");
    }
}
