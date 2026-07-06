using AuthReference.Domain.Models.Responses;
using MediatR;

namespace AuthReference.Application.Commands.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<LoginResponse>;
