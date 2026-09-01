using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PPECB.Domain.Exceptions;
using ValidationException = PPECB.Domain.Exceptions.ValidationException;

namespace PPECB.API.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 problem responses. Centralising this keeps the
/// controllers free of try/catch and guarantees that an unexpected failure never leaks a
/// stack trace or SQL detail to the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // Too late to replace the response; let it fail rather than corrupting output.
            _logger.LogError(exception, "Exception thrown after the response had started.");
            throw exception;
        }

        var problem = BuildProblem(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request rejected ({Status}): {Message}",
                problem.Status, exception.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        // Serialize against the runtime type. Passing the ProblemDetails-typed variable
        // would drop ValidationProblemDetails.Errors, losing the per-field messages.
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, problem.GetType(), SerializerOptions));
    }

    private ProblemDetails BuildProblem(Exception exception) => exception switch
    {
        ValidationException validation => new ValidationProblemDetails(validation.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        },

        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found",
            Detail = notFound.Message
        },

        ConcurrencyConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = conflict.Message
        },

        DuplicateKeyException duplicate => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = duplicate.Message
        },

        BusinessRuleException rule => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request could not be completed",
            Detail = rule.Message
        },

        UnauthorizedAccessException => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Authentication is required to access this resource."
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            // Detail is only revealed outside Production; otherwise it could disclose
            // connection strings, file paths or schema information.
            Detail = _environment.IsProduction()
                ? "Please try again. If the problem persists, contact support."
                : exception.ToString()
        }
    };
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
