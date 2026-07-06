namespace AuthReference.Domain.Models.Responses;

public record RegisterResponse(Guid UserId, string Email, TokenPair Tokens);
