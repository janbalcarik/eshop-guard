using EshopGuard.Jobs.Evidence;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Protocols;
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
        services.AddNotifications();
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
        services.TryAddScoped<FixGroupService>();
        services.TryAddScoped<PublicationService>();
        services.TryAddScoped<ProtocolNumberAllocator>();
        services.TryAddScoped<ProtocolDocumentBuilder>();
        services.TryAddScoped<ProtocolService>();
        services.TryAddScoped<Runs.RunQueryService>();
        services.TryAddScoped<Runs.RunCancelService>();
        services.TryAddScoped<Runs.ShopChangeReader>();
        services.AddOptions<Runs.SseOptions>().BindConfiguration(Runs.SseOptions.SectionName);
        services.TryAddSingleton<Runs.RunEventStream>();
        services.AddHostedService(sp => sp.GetRequiredService<Runs.RunEventStream>());
        services.TryAddScoped<GenerationBudget>();
        services.TryAddScoped<QuestionService>();
        services.TryAddScoped<Notifications.NotificationService>();
        services.AddEvidenceOptions();
        services.TryAddScoped<Evidence.EvidenceService>();
        return services;
    }
}
