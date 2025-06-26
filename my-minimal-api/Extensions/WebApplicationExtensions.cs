using Microsoft.EntityFrameworkCore;
using MyMinimalApi.Services;
using MyMinimalApi.Models;
using MyMinimalApi.Middleware;
using MyMinimalApi.Hubs;
using Serilog;

namespace MyMinimalApi.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigureTodoApp(this WebApplication app)
    {
        // Configure static files and middleware
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseMiddleware<UserActionLoggingMiddleware>();
        
        // Seed data
        app.SeedTodoData();
        
        // Map SignalR hub
        app.MapHub<TodoHub>("/todoHub");
        
        return app;
    }
    
    private static void SeedTodoData(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
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
}
