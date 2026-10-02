using System.Text.Json.Nodes;
using EshopGuard.Api.Problems;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EshopGuard.Api.OpenApi;

/// <summary>
/// Adds <c>x-problem-codes</c> to every operation (AD 12): the codes declared with <c>.ProducesProblemCodes(...)</c> on the
/// endpoint and its groups. The frontend generates its list of texts to translate from it.
/// </summary>
internal sealed class ProblemCodesOperationTransformer : IOpenApiOperationTransformer
{
    public const string Extension = "x-problem-codes";

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        var codes = context.Description.ActionDescriptor.EndpointMetadata.OfType<ProblemCodesMetadata>()
            .SelectMany(m => m.Codes).Distinct().Order(StringComparer.Ordinal).ToList();
        if (codes.Count > 0)
        {
            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            operation.Extensions[Extension] = new JsonNodeExtension(new JsonArray(codes.Select(c => (JsonNode)JsonValue.Create(c)).ToArray()));
        }

        return Task.CompletedTask;
    }
}
