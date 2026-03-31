using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MeowAutoChrome.Web.Middleware;

/// <summary>
/// 将未处理的异常转换为 RFC 7807 Problem Details 的中间件并写入响应。<br/>
/// Middleware that translates unhandled exceptions into RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// 创建中间件实例并注入依赖。<br/>
    /// Create a middleware instance and inject dependencies.
    /// </summary>
    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// 捕获后续中间件/请求处理中的异常，并将其转换为 ProblemDetails 响应。<br/>
    /// Capture exceptions from downstream middleware/request handling and convert them to ProblemDetails responses.
    /// </summary>
    /// <param name="context">HTTP 上下文 / HTTP context.</param>
    /// <returns>处理请求的任务 / task representing request handling.</returns>
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
