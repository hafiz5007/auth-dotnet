using AuthReference.Domain.Models.Responses;
using MediatR;

namespace AuthReference.Application.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;
