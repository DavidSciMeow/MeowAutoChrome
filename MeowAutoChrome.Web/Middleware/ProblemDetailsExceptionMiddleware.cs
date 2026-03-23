using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MeowAutoChrome.Web.Middleware;

public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var status = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var title = status switch
        {
            400 => "InvalidRequest",
            403 => "Forbidden",
            404 => "NotFound",
            500 => "ServerError",
            _ => "Error"
        };

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Detail = exception.Message,
            Status = status,
            Instance = context.TraceIdentifier
        };

        // Add standardized errorCode for clients to handle specific errors programmatically
        problem.Extensions["errorCode"] = title;

        if (_env.IsDevelopment())
        {
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}
