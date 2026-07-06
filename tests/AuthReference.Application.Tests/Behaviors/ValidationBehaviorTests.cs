using AuthReference.Application.Behaviors;
using AuthReference.Application.Commands.Login;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace AuthReference.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task InvalidRequest_ThrowsValidationException_BeforeCallingNext()
    {
        var validators = new IValidator<LoginCommand>[] { new LoginCommandValidator() };
        var sut = new ValidationBehavior<LoginCommand, string>(validators);

        var nextCalled = false;
        Task<string> Next() { nextCalled = true; return Task.FromResult("never"); }

        var act = () => sut.Handle(new LoginCommand("not-an-email", ""), Next, default);

        await act.Should().ThrowAsync<ValidationException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ValidRequest_CallsNext()
    {
        var validators = new IValidator<LoginCommand>[] { new LoginCommandValidator() };
        var sut = new ValidationBehavior<LoginCommand, string>(validators);

        Task<string> Next() => Task.FromResult("ok");

        var result = await sut.Handle(new LoginCommand("alice@example.com", "secret"), Next, default);

        result.Should().Be("ok");
    }
}
