using FluentValidation;

namespace AuthReference.Application.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);         // RFC 3696 upper bound

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(1)            // real strength check happens at register time
            .MaximumLength(200);         // hard cap so an attacker cannot burn CPU on a 5MB password
    }
}
