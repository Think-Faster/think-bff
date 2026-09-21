using System.Text.Json;
using BFF.Application.Exceptions;
using BFF.Contracts.Common;
using FluentValidation;

namespace BFF.WebApi.Middleware;

/// <summary>Single place that turns exceptions into the `{ code, message, details }` error body
/// (section 10). Domain exceptions map to their specific status/code; anything else is a 500.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message);
        }
        catch (CycleDetectedException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(context, StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message);
        }
        catch (ValidationException ex)
        {
            var details = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await WriteAsync(context, StatusCodes.Status400BadRequest, "validation_failed", "Request validation failed.", details);
        }
        catch (ArgumentException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "bad_request", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message, object? details = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var body = new ErrorResponse { Code = code, Message = message, Details = details };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
