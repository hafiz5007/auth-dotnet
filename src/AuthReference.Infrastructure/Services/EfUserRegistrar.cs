using AuthReference.Domain.Entities;
using AuthReference.Domain.Models.Requests;
using AuthReference.Domain.Services;
using AuthReference.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthReference.Infrastructure.Services;

public sealed class EfUserRegistrar : IUserRegistrar
{
    private readonly AppDbContext _db;
    private readonly IPasswordAuthenticator _passwords;
    private readonly IClock _clock;

    public EfUserRegistrar(AppDbContext db, IPasswordAuthenticator passwords, IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _clock = clock;
    }

    public async Task<RegisterOutcome> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalisedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == normalisedEmail, ct);
        if (exists)
            return RegisterOutcome.Failed("email already registered");

        var user = new ApplicationUser
        {
            Email = normalisedEmail,
            PasswordHash = _passwords.Hash(request.Password),
            DisplayName = request.DisplayName?.Trim(),
            EmailVerified = false,
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return RegisterOutcome.Ok(user);
    }
}
