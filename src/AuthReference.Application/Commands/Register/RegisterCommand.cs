using AuthReference.Domain.Models.Responses;
using MediatR;

namespace AuthReference.Application.Commands.Register;

public record RegisterCommand(string Email, string Password, string? DisplayName)
    : IRequest<RegisterResponse>;
