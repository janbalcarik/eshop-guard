using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using EshopGuard.Application.Problems;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Problems;

/// <summary>
/// Body of every error (AD 12): <c>application/problem+json</c> with a code instead of a sentence. <c>type</c> is
/// <c>urn:eshopguard:problem:{code}</c>, <c>title</c> the code again, <c>params</c> the values the frontend puts into its
/// text, <c>errors</c> the codes by field of <c>validation.failed</c>, <c>traceId</c> for the support. No <c>detail</c>.
/// </summary>
public sealed record EgProblemDto(
    string Type,
    string Title,
    int Status,
    string Code,
    IReadOnlyDictionary<string, object?> Params,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors,
    string? TraceId);

/// <summary>Writes <see cref="EgProblemDto"/>.</summary>
public static class EgProblem
{
    public const string ContentType = "application/problem+json";

    public static EgProblemDto Create(HttpContext context, string code, int status, IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new EgProblemDto(
            "urn:eshopguard:problem:" + code, code, status, code, parameters ?? new Dictionary<string, object?>(), errors,
            Activity.Current?.Id ?? context.TraceIdentifier);
    }

    public static EgProblemDto From(HttpContext context, DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Create(context, exception.Code, exception.Status, exception.Parameters, exception.Errors);
    }

    /// <summary>The problem as a result of an endpoint or a filter.</summary>
    public static IResult Result(HttpContext context, string code, int status, IReadOnlyDictionary<string, object?>? parameters = null) =>
        TypedResults.Json(Create(context, code, status, parameters), Options(context), ContentType, status);

    public static IResult Result(HttpContext context, DomainException exception) =>
        TypedResults.Json(From(context, exception), Options(context), ContentType, exception.Status);

    /// <summary>Writes the problem to the response (middleware, events of the cookie).</summary>
    public static async Task WriteAsync(HttpContext context, EgProblemDto problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);
        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = ContentType;
        if (problem.Status == StatusCodes.Status429TooManyRequests && problem.Params.TryGetValue("retryAfterSeconds", out var retry) && retry is not null)
        {
            context.Response.Headers.RetryAfter = Convert.ToString(retry, System.Globalization.CultureInfo.InvariantCulture);
        }

        await JsonSerializer.SerializeAsync(context.Response.Body, problem, Options(context), context.RequestAborted).ConfigureAwait(false);
    }

    private static JsonSerializerOptions Options(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
}
