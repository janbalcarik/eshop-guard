using EshopGuard.Api.Auth;
using EshopGuard.Api.Endpoints;
using EshopGuard.Api.Health;
using EshopGuard.Api.OpenApi;
using EshopGuard.Api.Problems;
using EshopGuard.Api.RateLimiting;
using EshopGuard.Api.Security;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs;
using EshopGuard.Storage;
using EshopGuard.Storage.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

// The lines "Request starting …" carry paths with query strings (the callback of Google): never logged (AD 13).
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

// The API always connects as eshopguard_app; the role is fixed here, not in configuration.
builder.Services.AddEshopGuardData(DatabaseRole.App);
builder.Services.AddEshopGuardStorage();

// The API only enqueues and cancels jobs; the worker processes them.
builder.Services.AddEshopGuardJobQueue();

// Identity, session, CSRF, Google and the services of change 9 (EshopGuard.Application).
builder.Services.AddEshopGuardIdentity(builder.Configuration);

// E-shops and the onboarding (change 10): the rules of the worker, the runs of change 8 and the policy of ownership.
builder.Services.AddEshopGuardShops();
builder.Services.AddEshopGuardFindings();
builder.Services.AddSingleton<EshopGuard.Api.Http.BlobLinks>();
builder.Services.AddScoped<SessionWriter>();
builder.Services.AddAuthorization();
builder.Services.AddEshopGuardRateLimiter(builder.Configuration);
builder.Services.AddExceptionHandler<EgExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<EndpointPolicyValidator>();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
    {
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
});
builder.Services.AddOpenApi("v1", o => o.AddOperationTransformer<ProblemCodesOperationTransformer>());
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck.Connection>("database")
    .AddCheck<DatabaseHealthCheck.Role>("database_role")
    .AddCheck<DatabaseHealthCheck.Migrations>("migrations")
    .AddCheck<StorageHealthCheck>("storage");

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<HttpsRequiredMiddleware>();
app.UseStatusCodePages(context =>
{
    var http = context.HttpContext;
    var code = http.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ProblemCodes.NotFound,
        StatusCodes.Status401Unauthorized => ProblemCodes.AuthUnauthenticated,
        StatusCodes.Status500InternalServerError => ProblemCodes.InternalError,
        _ => ProblemCodes.RequestInvalid,
    };
    return EgProblem.WriteAsync(http, EgProblem.Create(http, code, http.Response.StatusCode));
});
app.UseAuthentication();
app.UseMiddleware<HttpUserContextMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

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
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

// Every endpoint under /api: errors as codes, CSRF on every change (also before sign-in), policies checked at start.
var api = app.MapGroup("/api").RequireCsrf()
    .ProducesProblemCodes(ProblemCodes.InternalError, ProblemCodes.RequestInvalid, ProblemCodes.RateLimited, ProblemCodes.HttpsRequired);
api.MapAuthEndpoints();
api.MapMeEndpoints();
api.MapTenantsEndpoints();
var tenant = api.MapTenantGroup();
tenant.MapShopEndpoints().MapOnboardingEndpoints().MapOwnershipAndSettingsEndpoints();
tenant.MapShopWorkGroup().MapFindingEndpoints().MapFixEndpoints().MapQuestionEndpoints().MapFixGroupEndpoints().MapPublicationEndpoints().MapProtocolEndpoints();
tenant.MapNotificationEndpoints().MapEvidenceEndpoints();
api.MapInvitationEndpoints();
api.MapRefEndpoints();
api.MapCatalogEndpoints();
EshopGuard.Api.Http.FileEndpoints.MapFileEndpoints(api);

// A refused start (DatabaseStartupException, OptionsValidationException) propagates: the host has logged its code,
// the exception message is only the code, and the process ends with a non-zero exit code before Kestrel listens.
// (Not caught here, because WebApplicationFactory in the tests observes the entry point's exception.)
await app.RunAsync();

/// <summary>Entry point; public for <c>WebApplicationFactory</c> in the tests.</summary>
public partial class Program;
