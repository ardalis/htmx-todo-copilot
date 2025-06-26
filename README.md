# htmx-todo-copilot
A simple HTMX enabled TODO app built by/with GitHub Copilot and VS Code.

## Adding Toast Notifications

![image](https://github.com/user-attachments/assets/ce161e30-92d7-4be4-b9f0-b064396e8655)

## Log output Sample

![image](https://github.com/user-attachments/assets/f0a67e97-1d51-4966-bbbc-37d6b55f6e47)

## Dealing with Duplication

![image](https://github.com/user-attachments/assets/15ca6e1c-a9e6-4101-9e91-b1bd9b985564)

Initial middleware:

```csharp
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
```

## SignalR

![image](https://github.com/user-attachments/assets/a6de87a2-77cd-4e20-89d8-7320f031b241)

```text
Let me implement this step by step:

Now let's create a SignalR Hub for real-time communication:

Created TodoHub.cs

Now let's create a service to handle SignalR notifications:

Created TodoNotificationService.cs

Now let's update Program.cs to register SignalR and the notification service:

Program.cs+1-0
Program.cs+2-0
Now let's add the SignalR hub to the pipeline:

Read Program.cs, lines 140 to 150

Let me find where the app.Run() is and add the hub mapping before it:

Read Program.cs, lines 257 to 267

Let me find where the endpoints end:

Read Program.cs, lines 240 to 250

Program.cs+3-0
Now let's update the endpoints to use the notification service. First, let's update the POST endpoint:

Program.cs+4-1
Now let's update the PUT endpoint:

Program.cs+4-1
Now let's update the DELETE endpoint:

Program.cs+5-1
Now let's update the HTML to include SignalR JavaScript client and handle real-time updates:

Program.cs+65-14
Summarized conversation history
```

Here's the new service it created:

```csharp
using Microsoft.AspNetCore.SignalR;
using MyMinimalApi.Hubs;
using MyMinimalApi.Models;

namespace MyMinimalApi.Services;

public interface ITodoNotificationService
{
    Task NotifyTodoAdded(TodoItem todo);
    Task NotifyTodoToggled(TodoItem todo);
    Task NotifyTodoDeleted(int todoId, string title);
}

public class TodoNotificationService : ITodoNotificationService
{
    private readonly IHubContext<TodoHub> _hubContext;

    public TodoNotificationService(IHubContext<TodoHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTodoAdded(TodoItem todo)
    {
        await _hubContext.Clients.All.SendAsync("TodoAdded", new
        {
            id = todo.Id,
            title = todo.Title,
            isCompleted = todo.IsCompleted,
            createdAt = todo.CreatedAt,
            html = GenerateTodoItemHtml(todo)
        });
    }

    public async Task NotifyTodoToggled(TodoItem todo)
    {
        await _hubContext.Clients.All.SendAsync("TodoToggled", new
        {
            id = todo.Id,
            title = todo.Title,
            isCompleted = todo.IsCompleted,
            html = GenerateTodoItemHtml(todo)
        });
    }

    public async Task NotifyTodoDeleted(int todoId, string title)
    {
        await _hubContext.Clients.All.SendAsync("TodoDeleted", new
        {
            id = todoId,
            title = title
        });
    }

    private static string GenerateTodoItemHtml(TodoItem todo)
    {
        return $"""
            <div class="todo-item" id="todo-{todo.Id}">
                <input type="checkbox" {(todo.IsCompleted ? "checked" : "")} 
                       hx-put="/todos/{todo.Id}/toggle" 
                       hx-target="#todo-{todo.Id}" 
                       hx-swap="outerHTML" />
                <span class="{(todo.IsCompleted ? "completed" : "")}">{todo.Title}</span>
                <button hx-delete="/todos/{todo.Id}" 
                        hx-target="#todo-{todo.Id}" 
                        hx-swap="outerHTML" 
                        class="delete-btn">Delete</button>
            </div>
        """;
    }
}
```

Initial working version between browsers instances:

![image](https://github.com/user-attachments/assets/5dd3fc1d-f0bc-44e1-b989-9f676ae5f3e8)


