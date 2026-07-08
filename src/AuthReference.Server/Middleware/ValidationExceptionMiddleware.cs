using System.Net;
using System.Text.Json;
using FluentValidation;

namespace AuthReference.Server.Middleware;

/// <summary>
/// Catches <see cref="ValidationException"/> thrown by the Application layer's
/// ValidationBehavior pipeline and maps it to <c>400 Bad Request</c> with a
/// problem+json body listing each failure. Every other exception falls through
/// to the framework's default handler.
/// </summary>
public sealed class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException vex)
        {
            var errors = vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/problem+json";

            var body = new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc7807",
                title = "Validation failed",
                status = 400,
                errors
            };
            await JsonSerializer.SerializeAsync(context.Response.Body, body);
        }
    }
}
