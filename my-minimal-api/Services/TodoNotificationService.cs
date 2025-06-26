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
