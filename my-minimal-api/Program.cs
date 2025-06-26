using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyMinimalApi.Models;
using MyMinimalApi.Services;
using MyMinimalApi.Middleware;
using MyMinimalApi.Hubs;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/todo-app-.txt", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services
builder.Services.AddDbContext<TodoContext>(options =>
    options.UseInMemoryDatabase("TodoDb"));
builder.Services.AddAntiforgery();
builder.Services.AddSignalR();
builder.Services.AddScoped<ITodoNotificationService, TodoNotificationService>();

var app = builder.Build();

// Configure static files
app.UseStaticFiles();
app.UseAntiforgery();

// Add our custom user action logging middleware
app.UseMiddleware<UserActionLoggingMiddleware>();

// Seed some sample data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TodoContext>();
    if (!context.TodoItems.Any())
    {
        Log.Information("🌱 Seeding database with initial todo items");
        context.TodoItems.AddRange(
            new TodoItem { Title = "Learn HTMX", IsCompleted = false },
            new TodoItem { Title = "Build Todo App", IsCompleted = false },
            new TodoItem { Title = "Deploy to Azure", IsCompleted = false }
        );
        context.SaveChanges();
        Log.Information("✅ Database seeded with {TodoCount} initial items", 3);
    }
    else
    {
        Log.Information("📋 Database already contains {ExistingTodoCount} todo items", context.TodoItems.Count());
    }
}

// Main page route
app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html>
<head>
    <title>HTMX Todo App</title>
    <script src="https://unpkg.com/htmx.org@1.9.9"></script>
    <script src="https://unpkg.com/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
    <link href="/css/site.css" rel="stylesheet" />
</head>
<body>
    <div class="container">
        <h1>Todo Application</h1>
        <div class="connection-status" id="connection-status">🔄 Connecting...</div>
        <div id="todo-app" hx-get="/todos" hx-trigger="load"></div>
    </div>
    
    <!-- Toast Container -->
    <div id="toast-container" class="toast-container"></div>
    
    <script>
        // SignalR Connection
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/todoHub")
            .build();

        // Connection status
        const statusElement = document.getElementById('connection-status');
        
        connection.start().then(() => {
            console.log('SignalR Connected');
            statusElement.innerHTML = '🟢 Live Updates Active';
            statusElement.style.color = '#28a745';
        }).catch(err => {
            console.error('SignalR Connection Error: ', err);
            statusElement.innerHTML = '🔴 Connection Failed';
            statusElement.style.color = '#dc3545';
        });

        // Toast notification function
        function showToast(message, type = 'success') {
            console.log('Showing toast:', message, type);
            const container = document.getElementById('toast-container');
            const toast = document.createElement('div');
            toast.className = `toast ${type}`;
            toast.textContent = message;
            
            container.appendChild(toast);
            
            setTimeout(() => toast.classList.add('show'), 100);
            
            setTimeout(() => {
                toast.classList.remove('show');
                setTimeout(() => {
                    if (container.contains(toast)) {
                        container.removeChild(toast);
                    }
                }, 300);
            }, 3000);
        }

        // SignalR Event Handlers
        connection.on("TodoAdded", function (data) {
            console.log('Todo added via SignalR:', data);
            
            // Add the new todo to the list if it doesn't exist
            if (!document.getElementById(`todo-${data.id}`)) {
                const todoList = document.getElementById('todo-list');
                if (todoList) {
                    todoList.insertAdjacentHTML('beforeend', data.html);
                }
            }
            
            showToast(`"${data.title}" was added by another user!`, 'info');
        });

        connection.on("TodoToggled", function (data) {
            console.log('Todo toggled via SignalR:', data);
            
            const todoElement = document.getElementById(`todo-${data.id}`);
            if (todoElement) {
                todoElement.outerHTML = data.html;
            }
            
            const status = data.isCompleted ? 'completed' : 'reopened';
            showToast(`"${data.title}" was ${status} by another user!`, 'info');
        });

        connection.on("TodoDeleted", function (data) {
            console.log('Todo deleted via SignalR:', data);
            
            const todoElement = document.getElementById(`todo-${data.id}`);
            if (todoElement) {
                todoElement.remove();
            }
            
            showToast(`"${data.title}" was deleted by another user!`, 'info');
        });

        // Test function
        window.testToast = () => showToast('Test notification!', 'success');
        
        // HTMX event handlers for local actions
        document.body.addEventListener('htmx:afterRequest', function(event) {
            console.log('HTMX afterRequest event:', event.detail);
            
            const xhr = event.detail.xhr;
            const requestConfig = event.detail.requestConfig;
            
            if (xhr.status === 200 && requestConfig) {
                const method = requestConfig.verb.toUpperCase();
                const url = requestConfig.path;
                
                console.log('Request details:', method, url);
                
                if (method === 'POST' && url === '/todos') {
                    showToast('Todo item added successfully!', 'success');
                } else if (method === 'DELETE' && url.startsWith('/todos/')) {
                    showToast('Todo item deleted!', 'success');
                } else if (method === 'PUT' && url.includes('/toggle')) {
                    showToast('Todo item updated!', 'success');
                }
            }
        });
        
        // Clear form after successful POST
        document.body.addEventListener('htmx:afterRequest', function(event) {
            const xhr = event.detail.xhr;
            const requestConfig = event.detail.requestConfig;
            
            if (xhr.status === 200 && 
                requestConfig.verb === 'POST' && 
                requestConfig.path === '/todos') {
                const form = event.detail.elt;
                if (form.tagName === 'FORM') {
                    form.reset();
                }
            }
        });
    </script>
</body>
</html>
""", "text/html"));

// API endpoints
app.MapGet("/todos", async (TodoContext db) =>
{
    var todos = await db.TodoItems.OrderBy(t => t.CreatedAt).ToListAsync();
    var html = $"""
        <div class="todo-container">
            <form hx-post="/todos" hx-target="#todo-list" hx-swap="beforeend">
                <input type="text" name="title" placeholder="Add new todo..." required />
                <button type="submit">Add</button>
            </form>
            <div id="todo-list">
                {string.Join("", todos.Select(todo => $"""
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
                """))}
            </div>
        </div>
    """;
    return Results.Content(html, "text/html");
});

app.MapPost("/todos", async (TodoContext db, IFormCollection form, ITodoNotificationService notificationService) =>
{
    var title = form["title"].ToString();
    
    if (string.IsNullOrWhiteSpace(title))
        return Results.BadRequest();

    var todo = new TodoItem { Title = title };
    db.TodoItems.Add(todo);
    await db.SaveChangesAsync();
    
    // Notify all connected clients
    await notificationService.NotifyTodoAdded(todo);

    var html = $"""
        <div class="todo-item" id="todo-{todo.Id}">
            <input type="checkbox" hx-put="/todos/{todo.Id}/toggle" 
                   hx-target="#todo-{todo.Id}" 
                   hx-swap="outerHTML" />
            <span>{todo.Title}</span>
            <button hx-delete="/todos/{todo.Id}" 
                    hx-target="#todo-{todo.Id}" 
                    hx-swap="outerHTML" 
                    class="delete-btn">Delete</button>
        </div>
    """;
    return Results.Content(html, "text/html");
}).DisableAntiforgery();

app.MapPut("/todos/{id}/toggle", async (int id, TodoContext db, ITodoNotificationService notificationService) =>
{
    var todo = await db.TodoItems.FindAsync(id);
    if (todo == null)
        return Results.NotFound();

    todo.IsCompleted = !todo.IsCompleted;
    await db.SaveChangesAsync();
    
    // Notify all connected clients
    await notificationService.NotifyTodoToggled(todo);

    var html = $"""
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
    return Results.Content(html, "text/html");
});

app.MapDelete("/todos/{id}", async (int id, TodoContext db, ITodoNotificationService notificationService) =>
{
    var todo = await db.TodoItems.FindAsync(id);
    if (todo == null)
        return Results.NotFound();

    var todoTitle = todo.Title; // Store title before deletion
    db.TodoItems.Remove(todo);
    await db.SaveChangesAsync();
    
    // Notify all connected clients
    await notificationService.NotifyTodoDeleted(id, todoTitle);

    return Results.Content("", "text/html");
});

// Map SignalR hub
app.MapHub<TodoHub>("/todoHub");

// Log application startup
Log.Information("🚀 HTMX Todo Application starting up...");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 Application terminated unexpectedly");
}
finally
{
    Log.Information("🛑 HTMX Todo Application shutting down...");
    Log.CloseAndFlush();
}