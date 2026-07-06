using FluentValidation;

namespace AuthReference.Application.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        // Password strength — deliberately modest. Length is the dominant factor;
        // adding "must contain a symbol" rules pushes users into predictable patterns.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .MaximumLength(120)
            .When(x => x.DisplayName is not null);
    }
}
