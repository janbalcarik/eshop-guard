using System.Reflection;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Codes of the notes, warnings and reasons of the tool. Every code has a text in <c>rules/texts/&lt;locale&gt;/_engine.yaml</c>
/// of every language (checked by the completeness test); the library never returns the sentence itself.
/// </summary>
internal static class EngineCodes
{
    // Notes of site_presence rules.
    public const string PresenceClosestParagraph = "presence_closest_paragraph";
    public const string PresencePatternMissing = "presence_pattern_missing";
    public const string PresenceFoundButPatternMissing = "presence_found_but_pattern_missing";
    public const string PresenceParagraphsNotEvaluated = "presence_paragraphs_not_evaluated";
    public const string UnreadPdfDocuments = "unread_pdf_documents";
    public const string LegalPagesNotLoaded = "legal_pages_not_loaded";
    public const string PagesNotLoaded = "pages_not_loaded";

    // Notes of site_signal rules.
    public const string SignalNotFound = "signal_not_found";
    public const string SignalCartNotDownloaded = "signal_cart_not_downloaded";
    public const string SignalFoundOn = "signal_found_on";

    // Notes of claim_list_match and label_notes checks.
    public const string ClaimListItem = "claim_list_item";
    public const string ClaimListSince = "claim_list_since";
    public const string ClaimListRemark = "claim_list_remark";
    public const string ClaimListOtherCategory = "claim_list_other_category";
    public const string ClaimListNoCategory = "claim_list_no_category";
    public const string ClaimListNotInList = "claim_list_not_in_list";
    public const string LabelNote = "label_note";

    // Notes of verdicts and parts of sentences.
    public const string EffectiveFrom = "effective_from";
    public const string ListMore = "list_more";

    // Status of legal references.
    public const string ToVerify = "to_verify";
    public const string ToComplete = "to_complete";

    // Warnings of the crawl.
    public const string RobotsHomeDisallowed = "robots_home_disallowed";
    public const string RobotsUnreachable = "robots_unreachable";
    public const string SitemapUnreadable = "sitemap_unreadable";
    public const string SitemapInvalid = "sitemap_invalid";
    public const string SitemapTooMany = "sitemap_too_many";
    public const string SsrfBlocked = "ssrf_blocked";
    public const string SsrfBlockedUrls = "ssrf_blocked_urls";
    public const string PagesNotProcessed = "pages_not_processed";
    public const string NoResponse = "no_response";

    // Warnings of the analysis.
    public const string NoLegalPages = "no_legal_pages";
    public const string TextNotLoaded = "text_not_loaded";
    public const string MostlyScriptRendered = "mostly_script_rendered";
    public const string ProfileFailedFatal = "profile_failed_fatal";
    public const string ProfileFailed = "profile_failed";
    public const string ProfileNoFit = "profile_no_fit";
    public const string ProfilesNotCreated = "profiles_not_created";
    public const string ModelMock = "model_mock";
    public const string ModelMissingKey = "model_missing_key";
    public const string SieveUnevaluated = "sieve_unevaluated";
    public const string EvaluationNotConfirmed = "evaluation_not_confirmed";
    public const string EvaluationNotConfirmedTexts = "evaluation_not_confirmed_texts";
    public const string JevErrors = "jev_errors";
    public const string ModuleOtherJurisdiction = "module_other_jurisdiction";
    public const string ModuleNoRuleSet = "module_no_rule_set";
    public const string ModuleDisabled = "module_disabled";

    // Reasons of obligations that were not checked and of modules that did not run.
    public const string EvaluationSkipped = "evaluation_skipped";
    public const string SiteSignalsNotEvaluated = "site_signals_not_evaluated";
    public const string LegalTextsNotGiven = "legal_texts_not_given";
    public const string SiteNotCrawled = "site_not_crawled";
    public const string NoRulesForJurisdiction = "no_rules_for_jurisdiction";
    public const string RuleSetDisabled = "rule_set_disabled";

    /// <summary>Every code above.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(EngineCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();
}
