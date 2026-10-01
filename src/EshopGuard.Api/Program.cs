using EshopGuard.Api.Health;
using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Storage;
using EshopGuard.Storage.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

// The API always connects as eshopguard_app; the role is fixed here, not in configuration.
builder.Services.AddEshopGuardData(DatabaseRole.App);
builder.Services.AddEshopGuardStorage();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status >= 500 ? "error.unexpected" : "error.request"));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck.Connection>("database")
    .AddCheck<DatabaseHealthCheck.Role>("database_role")
    .AddCheck<DatabaseHealthCheck.Migrations>("migrations")
    .AddCheck<StorageHealthCheck>("storage");

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});

// A refused start (DatabaseStartupException, OptionsValidationException) propagates: the host has logged its code,
// the exception message is only the code, and the process ends with a non-zero exit code before Kestrel listens.
// (Not caught here, because WebApplicationFactory in the tests observes the entry point's exception.)
await app.RunAsync();

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in the tests.</summary>
public partial class Program;
