using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

/// <summary>
/// Global exception handler — maps domain exceptions to RFC 7807 ProblemDetails responses.
/// Replaces scattered try/catch blocks in controllers.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, title) = exception switch
        {
            InvalidOperationException    => (StatusCodes.Status400BadRequest,   "Bad Request"),
            UnauthorizedAccessException  => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException         => (StatusCodes.Status404NotFound,     "Not Found"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,     "Conflict"),
            _                            => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Handled exception ({Type}): {Message}", exception.GetType().Name, exception.Message);

        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
