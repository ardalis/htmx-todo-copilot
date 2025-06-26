using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyMinimalApi.Models;
using MyMinimalApi.Services;
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

var app = builder.Build();

// Configure static files
app.UseStaticFiles();
app.UseAntiforgery();

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
app.MapGet("/", (HttpContext context) => 
{
    var userAgent = context.Request.Headers.UserAgent.ToString();
    var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    Log.Information("🏠 User accessed main page from IP: {ClientIP}, UserAgent: {UserAgent}", clientIP, userAgent);
    
    return Results.Content("""
<!DOCTYPE html>
<html>
<head>
    <title>HTMX Todo App</title>
    <script src="https://unpkg.com/htmx.org@1.9.9"></script>
    <link href="/css/site.css" rel="stylesheet" />
</head>
<body>
    <div class="container">
        <h1>Todo Application</h1>
        <div id="todo-app" hx-get="/todos" hx-trigger="load"></div>
    </div>
    
    <!-- Toast Container -->
    <div id="toast-container" class="toast-container"></div>
    
    <script>
        // Toast notification function
        function showToast(message, type = 'success') {
            console.log('Showing toast:', message, type); // Debug log
            const container = document.getElementById('toast-container');
            const toast = document.createElement('div');
            toast.className = `toast ${type}`;
            toast.textContent = message;
            
            container.appendChild(toast);
            
            // Trigger animation
            setTimeout(() => toast.classList.add('show'), 100);
            
            // Auto-remove after 3 seconds
            setTimeout(() => {
                toast.classList.remove('show');
                setTimeout(() => {
                    if (container.contains(toast)) {
                        container.removeChild(toast);
                    }
                }, 300);
            }, 3000);
        }
        
        // Test function - you can call this in the browser console
        window.testToast = () => showToast('Test notification!', 'success');
        
        // Listen for HTMX events with better debugging
        document.body.addEventListener('htmx:afterRequest', function(event) {
            console.log('HTMX afterRequest event:', event.detail); // Debug log
            
            const xhr = event.detail.xhr;
            const requestConfig = event.detail.requestConfig;
            
            if (xhr.status === 200 && requestConfig) {
                const method = requestConfig.verb.toUpperCase();
                const url = requestConfig.path;
                
                console.log('Request details:', method, url); // Debug log
                
                if (method === 'POST' && url === '/todos') {
                    showToast('Todo item added successfully!', 'success');
                } else if (method === 'DELETE' && url.startsWith('/todos/')) {
                    showToast('Todo item deleted!', 'info');
                } else if (method === 'PUT' && url.includes('/toggle')) {
                    showToast('Todo item updated!', 'info');
                }
            }
        });
        
        // Alternative: Listen for specific HTMX events
        document.body.addEventListener('htmx:afterSwap', function(event) {
            console.log('HTMX afterSwap event:', event.detail); // Debug log
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
""", "text/html");
});

// API endpoints
app.MapGet("/todos", async (TodoContext db, HttpContext context) =>
{
    var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    Log.Information("📖 User {ClientIP} requested todo list", clientIP);
    
    var todos = await db.TodoItems.OrderBy(t => t.CreatedAt).ToListAsync();
    Log.Information("📋 Returning {TodoCount} todo items to user {ClientIP}", todos.Count, clientIP);
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

app.MapPost("/todos", async (TodoContext db, IFormCollection form, HttpContext context) =>
{
    var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var title = form["title"].ToString();
    
    Log.Information("➕ User {ClientIP} attempting to create todo with title: '{Title}'", clientIP, title);
    
    if (string.IsNullOrWhiteSpace(title))
    {
        Log.Warning("❌ User {ClientIP} attempted to create todo with empty title", clientIP);
        return Results.BadRequest();
    }

    var todo = new TodoItem { Title = title };
    db.TodoItems.Add(todo);
    await db.SaveChangesAsync();
    
    Log.Information("✅ User {ClientIP} successfully created todo item {TodoId}: '{Title}'", clientIP, todo.Id, todo.Title);

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

app.MapPut("/todos/{id}/toggle", async (int id, TodoContext db, HttpContext context) =>
{
    var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    Log.Information("🔄 User {ClientIP} attempting to toggle todo item {TodoId}", clientIP, id);
    
    var todo = await db.TodoItems.FindAsync(id);
    if (todo == null)
    {
        Log.Warning("❌ User {ClientIP} attempted to toggle non-existent todo item {TodoId}", clientIP, id);
        return Results.NotFound();
    }

    var oldStatus = todo.IsCompleted;
    todo.IsCompleted = !todo.IsCompleted;
    await db.SaveChangesAsync();
    
    Log.Information("✅ User {ClientIP} toggled todo item {TodoId} '{Title}' from {OldStatus} to {NewStatus}", 
        clientIP, todo.Id, todo.Title, oldStatus, todo.IsCompleted);

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

app.MapDelete("/todos/{id}", async (int id, TodoContext db, HttpContext context) =>
{
    var clientIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    Log.Information("🗑️ User {ClientIP} attempting to delete todo item {TodoId}", clientIP, id);
    
    var todo = await db.TodoItems.FindAsync(id);
    if (todo == null)
    {
        Log.Warning("❌ User {ClientIP} attempted to delete non-existent todo item {TodoId}", clientIP, id);
        return Results.NotFound();
    }

    var todoTitle = todo.Title; // Store title before deletion for logging
    db.TodoItems.Remove(todo);
    await db.SaveChangesAsync();
    
    Log.Information("🗑️ User {ClientIP} successfully deleted todo item {TodoId}: '{Title}'", clientIP, id, todoTitle);

    return Results.Content("", "text/html");
});

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