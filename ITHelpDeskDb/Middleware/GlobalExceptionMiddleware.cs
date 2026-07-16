using System.Net;
using System.Text.Json;

namespace ITHelpDeskDb.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        // Map specific exception types to HTTP status codes
        (int statusCode, string message) = ex switch
        {
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "You are not authorized to perform this action."),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "The requested resource was not found."),

            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                ex.Message),

            ArgumentException => (
                StatusCodes.Status400BadRequest,
                ex.Message),

            // Catch-all — never expose the real exception message in production
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred. Please try again.")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            message,
            statusCode,
            // Only include the stack trace in development
            detail = context.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment()
                    ? ex.ToString()
                    : null,
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}