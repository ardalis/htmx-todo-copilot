using Serilog;

namespace MyMinimalApi.Middleware;

public class UserActionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Serilog.ILogger _logger;

    public UserActionLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
        _logger = Log.ForContext<UserActionLoggingMiddleware>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var method = context.Request.Method;
        var path = context.Request.Path;
        var userAgent = context.Request.Headers.UserAgent.ToString();

        // Log the incoming request with appropriate emoji
        var emoji = GetActionEmoji(method, path);
        var actionDescription = GetActionDescription(method, path);
        
        _logger.Information("{Emoji} User {ClientIP} {ActionDescription} - {Method} {Path}", 
            emoji, clientIP, actionDescription, method, path);

        // Capture request body for POST requests (for todo creation)
        string? requestBody = null;
        if (method == "POST" && path == "/todos")
        {
            requestBody = await CaptureRequestBody(context);
        }

        // Call the next middleware
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Log the response
            LogResponse(context, clientIP, method, path, requestBody);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "💥 Error processing request {Method} {Path} for user {ClientIP}", method, path, clientIP);
            throw;
        }
        finally
        {
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task<string?> CaptureRequestBody(HttpContext context)
    {
        try
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            return body;
        }
        catch
        {
            return null;
        }
    }

    private void LogResponse(HttpContext context, string clientIP, string method, string path, string? requestBody)
    {
        var statusCode = context.Response.StatusCode;
        var emoji = statusCode >= 200 && statusCode < 300 ? "✅" : "❌";

        if (method == "POST" && path == "/todos" && statusCode == 200)
        {
            // Extract title from form data for todo creation
            var title = ExtractTitleFromRequestBody(requestBody);
            _logger.Information("✅ User {ClientIP} successfully created todo: '{Title}'", clientIP, title);
        }
        else if (method == "PUT" && path.Contains("/toggle") && statusCode == 200)
        {
            var todoId = ExtractTodoIdFromPath(path);
            _logger.Information("✅ User {ClientIP} successfully toggled todo item {TodoId}", clientIP, todoId);
        }
        else if (method == "DELETE" && path.StartsWith("/todos/") && statusCode == 200)
        {
            var todoId = ExtractTodoIdFromPath(path);
            _logger.Information("✅ User {ClientIP} successfully deleted todo item {TodoId}", clientIP, todoId);
        }
        else if (statusCode >= 400)
        {
            _logger.Warning("❌ User {ClientIP} request failed - {Method} {Path} returned {StatusCode}", 
                clientIP, method, path, statusCode);
        }
    }

    private static string GetActionEmoji(string method, string path)
    {
        return (method, path) switch
        {
            ("GET", "/") => "🏠",
            ("GET", "/todos") => "📖",
            ("POST", "/todos") => "➕",
            ("PUT", var p) when p.Contains("/toggle") => "🔄",
            ("DELETE", var p) when p.StartsWith("/todos/") => "🗑️",
            _ => "🌐"
        };
    }

    private static string GetActionDescription(string method, string path)
    {
        return (method, path) switch
        {
            ("GET", "/") => "accessed main page",
            ("GET", "/todos") => "requested todo list",
            ("POST", "/todos") => "attempting to create todo",
            ("PUT", var p) when p.Contains("/toggle") => "attempting to toggle todo",
            ("DELETE", var p) when p.StartsWith("/todos/") => "attempting to delete todo",
            _ => $"made request to {path}"
        };
    }

    private static string? ExtractTitleFromRequestBody(string? requestBody)
    {
        if (string.IsNullOrEmpty(requestBody)) return "unknown";
        
        // Simple form parsing for title=value
        var titleMatch = System.Text.RegularExpressions.Regex.Match(requestBody, @"title=([^&]+)");
        return titleMatch.Success ? System.Web.HttpUtility.UrlDecode(titleMatch.Groups[1].Value) : "unknown";
    }

    private static int? ExtractTodoIdFromPath(string path)
    {
        var segments = path.Split('/');
        if (segments.Length >= 3 && int.TryParse(segments[2], out int id))
            return id;
        return null;
    }
}
