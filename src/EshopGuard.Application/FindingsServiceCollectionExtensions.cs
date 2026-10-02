using EshopGuard.Jobs.Fixes;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Fixes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EshopGuard.Application;

/// <summary>Registration of the services of change 11 (findings, fixes, evidence, protocols, notifications, runs).</summary>
public static class FindingsServiceCollectionExtensions
{
    /// <summary>
    /// The services of the findings and fixes in the API. Requires <c>AddEshopGuardShops</c> (the e-shops, the rules and their
    /// texts) and the file store (<c>IBlobStore</c>).
    /// </summary>
    public static IServiceCollection AddEshopGuardFindings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<FindingsOptions>().BindConfiguration(FindingsOptions.SectionName);
        services.TryAddScoped<ShopWorkLoader>();
        services.TryAddScoped<RuleTextCatalog>();
        services.TryAddScoped<ExtractContextReader>();
        services.TryAddScoped<AnswerPropagation>();
        services.TryAddScoped<DecisionMemoryWriter>();
        services.TryAddScoped<FindingTransitions>();
        services.TryAddScoped<PageWorkQueryService>();
        services.TryAddScoped<FindingQueryService>();
        services.TryAddScoped<FindingDecisionService>();
        services.TryAddScoped<OverviewService>();
        services.TryAddScoped<SearchService>();
        services.TryAddScoped<FindingsCsvExporter>();
        services.TryAddScoped<UserLocales>();
        services.AddOptions<FixesOptions>().BindConfiguration(FixesOptions.SectionName);
        services.TryAddScoped<PublishAvailability>();
        services.TryAddScoped<PageReviewService>();
        services.TryAddScoped<FixProposalService>();
        return services;
    }
}
