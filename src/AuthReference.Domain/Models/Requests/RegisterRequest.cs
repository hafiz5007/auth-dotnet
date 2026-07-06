namespace AuthReference.Domain.Models.Requests;

public record RegisterRequest(string Email, string Password, string? DisplayName);
