using MyMinimalApi.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from appsettings.json
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

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