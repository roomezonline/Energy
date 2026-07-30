using System.Net;
using System.Text.Json;
using EnergyMonitor.Application.Interfaces;

namespace EnergyMonitor.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex) when (!string.IsNullOrEmpty(ex.Message))
        {
            _log.LogWarning("Business logic error: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new { error = ex.Message });
            await context.Response.WriteAsync(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogWarning("Unauthorized access: {Message}", ex.Message);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new { error = "دسترسی غیرمجاز" });
            await context.Response.WriteAsync(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");

            if (context.Response.HasStarted) throw;

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var errMsg = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
                ? ex.ToString()
                : "خطای داخلی سرور";
            var result = JsonSerializer.Serialize(new { error = errMsg, type = ex.GetType().Name });
            await context.Response.WriteAsync(result);
        }
    }
}
