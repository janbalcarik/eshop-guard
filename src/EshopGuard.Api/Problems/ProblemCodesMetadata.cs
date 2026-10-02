using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Problems;

/// <summary>Codes of errors an endpoint may return (OpenAPI <c>x-problem-codes</c>, so the frontend knows what to translate).</summary>
public sealed class ProblemCodesMetadata(IReadOnlyList<string> codes)
{
    public IReadOnlyList<string> Codes { get; } = codes;
}

public static class ProblemCodesExtensions
{
    /// <summary>Declares the codes (and the problem response) of the endpoint; <c>validation.failed</c>, <c>csrf.invalid</c> etc. are added by the groups.</summary>
    public static TBuilder ProducesProblemCodes<TBuilder>(this TBuilder builder, params string[] codes)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithMetadata(new ProblemCodesMetadata(codes));
        return builder;
    }

    /// <summary>All codes declared on the endpoint and its groups, ordered.</summary>
    public static IReadOnlyList<string> ProblemCodes(this Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.Metadata.GetOrderedMetadata<ProblemCodesMetadata>().SelectMany(m => m.Codes).Distinct().Order(StringComparer.Ordinal).ToList();
    }
}
