using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyMinimalApi.Models;
using MyMinimalApi.Services;

namespace MyMinimalApi.Extensions;

public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/todos", GetTodos);
        endpoints.MapPost("/todos", CreateTodo).DisableAntiforgery();
        endpoints.MapPut("/todos/{id}/toggle", ToggleTodo);
        endpoints.MapDelete("/todos/{id}", DeleteTodo);
        
        return endpoints;
    }

    private static async Task<IResult> GetTodos(TodoContext db)
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
    }

    private static async Task<IResult> CreateTodo(TodoContext db, IFormCollection form, ITodoNotificationService notificationService)
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
    }

    private static async Task<IResult> ToggleTodo(int id, TodoContext db, ITodoNotificationService notificationService)
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
    }

    private static async Task<IResult> DeleteTodo(int id, TodoContext db, ITodoNotificationService notificationService)
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
    }
}
