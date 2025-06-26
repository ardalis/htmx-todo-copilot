using MyMinimalApi.Extensions;
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

// Configure logging
builder.Host.UseSerilog();

// Register services
builder.Services.AddTodoServices();

var app = builder.Build();

// Configure the application pipeline
app.ConfigureTodoApp();

// Map endpoints
app.MapPageEndpoints();
app.MapTodoEndpoints();

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