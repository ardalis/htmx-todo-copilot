using Microsoft.EntityFrameworkCore;
using MyMinimalApi.Services;
using MyMinimalApi.Hubs;

namespace MyMinimalApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTodoServices(this IServiceCollection services)
    {
        services.AddDbContext<TodoContext>(options =>
            options.UseInMemoryDatabase("TodoDb"));

        services.AddAntiforgery();
        services.AddSignalR();
        services.AddScoped<ITodoNotificationService, TodoNotificationService>();

        return services;
    }
}
