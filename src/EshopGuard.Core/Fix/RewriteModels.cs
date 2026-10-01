using EshopGuard.Core.Models;

namespace EshopGuard.Core.Fix;

/// <summary>
/// What to rewrite: the pages of a scan and its findings (usually read from <c>pages.jsonl</c> and <c>findings.json</c>).
/// </summary>
public sealed class RewriteInput
{
    /// <summary>Downloaded pages with their text.</summary>
    public required IReadOnlyList<RewritePageInput> Pages { get; init; }

    /// <summary>Findings of the scan; only segment findings of the groups porušení and k posouzení are rewritten.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>The jurisdiction when the shop sells in one country; the rewritten passages are checked by its rules.</summary>
    public string Country { get; init; } = "sk";

    /// <summary>Jurisdictions whose rules check the rewritten passages; empty means only <see cref="Country"/>.</summary>
    public IReadOnlyList<string> Jurisdictions { get; init; } = [];

    /// <summary>
    /// Language of the content of the shop: the texts of the findings in the prompt are in it. Null means the language of the
    /// laws of the first jurisdiction (a Slovak shop gets Slovak texts when they exist).
    /// </summary>
    public string? ContentLanguage { get; init; }

    /// <summary>The jurisdictions of the check: <see cref="Jurisdictions"/>, or <see cref="Country"/> alone.</summary>
    public IReadOnlyList<string> ResolvedJurisdictions => Jurisdictions.Count > 0 ? Jurisdictions.Distinct().ToList() : [Country];

    /// <summary>Rewrite at most this many pages (in URL order); null means all.</summary>
    public int? MaxPages { get; init; }
}

/// <summary>
/// A page of a scan with the texts the model may change.
/// </summary>
public sealed class RewritePageInput
{
    /// <summary>URL of the page.</summary>
    public required string Url { get; init; }

    /// <summary>Page type as written by the scan (<c>product</c>, <c>legal</c>, ...).</summary>
    public string Type { get; init; } = "";

    /// <summary>Content of the <c>title</c> element.</summary>
    public string? Title { get; init; }

    /// <summary>Title, h1, breadcrumb and category of the page.</summary>
    public string Category { get; init; } = "";

    /// <summary>Main text, one block per line.</summary>
    public string MainText { get; init; } = "";

    /// <summary>Meta description, when the scan stored it.</summary>
    public string? MetaDescription { get; init; }

    /// <summary>Product description from JSON-LD, when the scan stored it.</summary>
    public string? JsonLdDescription { get; init; }
}

/// <summary>
/// Estimate shown before the model is called.
/// </summary>
public sealed class RewriteEstimate
{
    /// <summary>Pages with at least one finding to rewrite.</summary>
    public int Pages { get; init; }

    /// <summary>Findings to rewrite.</summary>
    public int Findings { get; init; }

    /// <summary>Of <see cref="Pages"/>, pages whose rewrite is in the cache and costs nothing.</summary>
    public int CachedPages { get; init; }

    /// <summary>Estimated input tokens of the pages to send.</summary>
    public long EstimatedInputTokens { get; init; }

    /// <summary>Of <see cref="EstimatedInputTokens"/>, tokens of the shared prompt expected from the OpenAI cache.</summary>
    public long EstimatedCachedTokens { get; init; }

    /// <summary>Estimated output tokens, reasoning included.</summary>
    public long EstimatedOutputTokens { get; init; }

    /// <summary>Estimated price in USD.</summary>
    public decimal EstimatedCostUsd { get; init; }

    /// <summary>Model that will write the rewrite.</summary>
    public string Model { get; init; } = "";

    /// <summary>True when the mock client is used, so nothing is paid.</summary>
    public bool IsMock { get; init; }

    /// <summary>True when the estimated price exceeds the limit and the host must confirm.</summary>
    public bool RequiresConfirmation { get; init; }
}

/// <summary>
/// Result of a check of a rewritten passage by the rules of the tool.
/// </summary>
public enum RewriteStatus
{
    /// <summary>The rules find nothing in the new text (or the passage was deleted).</summary>
    Resolved,

    /// <summary>The new text contains a placeholder the shop must fill in; until then it must not be published.</summary>
    WaitingForFacts,

    /// <summary>The rules still find a violation or a passage to assess in the new text.</summary>
    StillFinding,

    /// <summary>The model kept the original wording and gave the reason (only for findings to assess, as a rule).</summary>
    Kept,

    /// <summary>The model neither changed nor kept the finding.</summary>
    NotAddressed,
}

/// <summary>
/// Result of the rewrite of the findings of a scan.
/// </summary>
public sealed class RewriteResult
{
    /// <summary>Pages in URL order.</summary>
    public IReadOnlyList<RewritePage> Pages { get; init; } = [];

    /// <summary>Tokens, price and counts.</summary>
    public RewriteStats Stats { get; init; } = new();

    /// <summary>Version of the prompt from <c>config/rewrite.yaml</c>.</summary>
    public string PromptVersion { get; init; } = "";

    /// <summary>Model that wrote the rewrite, as reported by the API.</summary>
    public string Model { get; init; } = "";

    /// <summary>Warnings for the user.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Rewrite of one page.
/// </summary>
public sealed class RewritePage
{
    /// <summary>URL of the page.</summary>
    public required string Url { get; init; }

    /// <summary>Findings on the page with their ids (F1, F2, ...) and what became of them.</summary>
    public IReadOnlyList<RewriteFinding> Findings { get; init; } = [];

    /// <summary>
    /// The whole text of the page block by block, before and after the changes (for a side-by-side or diff view):
    /// main text B1, B2, ... in page order, then TITLE, META, JSONLD or CHROME when a finding was there.
    /// </summary>
    public IReadOnlyList<RewriteBlock> Blocks { get; init; } = [];

    /// <summary>Changed passages.</summary>
    public IReadOnlyList<RewriteChange> Changes { get; init; } = [];

    /// <summary>Findings the model kept, with its reason.</summary>
    public IReadOnlyList<RewriteKept> Kept { get; init; } = [];

    /// <summary>True when the rewrite came from the cache.</summary>
    public bool FromCache { get; init; }

    /// <summary>Error of the page, if the model could not rewrite it.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// A block of the page text before and after the rewrite.
/// </summary>
public sealed class RewriteBlock
{
    /// <summary>Id of the block (B1, B2, ..., TITLE, META, JSONLD, CHROME).</summary>
    public required string Id { get; init; }

    /// <summary>Text on the page.</summary>
    public required string Original { get; init; }

    /// <summary>Text after the rewrite; the same as <see cref="Original"/> when unchanged, empty when deleted.</summary>
    public required string Rewritten { get; init; }

    /// <summary>True when a change touches the block.</summary>
    public bool Changed { get; init; }

    /// <summary>True for blocks of the main text (B1, B2, ...), false for title, meta description and similar.</summary>
    public bool IsMainText => Id.Length > 1 && Id[0] == 'B' && char.IsDigit(Id[1]);
}

/// <summary>
/// A finding on a rewritten page.
/// </summary>
public sealed class RewriteFinding
{
    /// <summary>Id within the page (F1, F2, ...).</summary>
    public required string Id { get; init; }

    /// <summary>The finding of the scan.</summary>
    public required Finding Finding { get; init; }

    /// <summary>Texts of every verdict of the finding in the language of the content, the strictest first.</summary>
    public IReadOnlyList<RewriteFindingTexts> Texts { get; init; } = [];

    /// <summary>Blocks of the page the finding is in (B1, ..., TITLE, META, JSONLD, CHROME).</summary>
    public IReadOnlyList<string> Blocks { get; init; } = [];

    /// <summary>Other pages with the same text, where the same change applies.</summary>
    public IReadOnlyList<string> AlsoOn { get; init; } = [];

    /// <summary>What became of the finding.</summary>
    public RewriteStatus Status { get; set; } = RewriteStatus.NotAddressed;
}

/// <summary>
/// A changed passage: the whole block before and after.
/// </summary>
public sealed class RewriteChange
{
    /// <summary>Blocks replaced by the new text (the first gets it, further ones are removed).</summary>
    public IReadOnlyList<string> BlockIds { get; init; } = [];

    /// <summary>The block as it is on the page.</summary>
    public string Original { get; init; } = "";

    /// <summary>New text of the block; empty means delete the block.</summary>
    public string Rewritten { get; init; } = "";

    /// <summary>Findings the change addresses.</summary>
    public IReadOnlyList<string> FindingIds { get; init; } = [];

    /// <summary>Facts the shop must fill in (the placeholders in the new text).</summary>
    public IReadOnlyList<string> Placeholders { get; init; } = [];

    /// <summary>Why the change is needed and why the new wording is lawful (Czech).</summary>
    public string Reason { get; init; } = "";

    /// <summary>Result of the check of the new text.</summary>
    public RewriteStatus Status { get; set; } = RewriteStatus.Resolved;

    /// <summary>Findings of the groups porušení and k posouzení the rules still make in the new text.</summary>
    public List<Finding> RemainingFindings { get; } = [];

    /// <summary>Findings to verify the new text raises, e.g. a named certification to check.</summary>
    public List<Finding> VerifyFindings { get; } = [];

    /// <summary>Findings of rules that apply only later; they do not change the status, the report mentions them.</summary>
    public List<Finding> UpcomingFindings { get; } = [];
}

/// <summary>
/// Texts of one verdict of a finding for the prompt and the report of a rewrite.
/// </summary>
public sealed class RewriteFindingTexts
{
    /// <summary>Jurisdiction of the verdict.</summary>
    public required string Jurisdiction { get; init; }

    /// <summary>Group of the verdict.</summary>
    public required string Checkability { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Explanation for the jurisdiction.</summary>
    public required string Explanation { get; init; }

    /// <summary>Recommendation.</summary>
    public required string Recommendation { get; init; }
}

/// <summary>
/// A finding the model kept.
/// </summary>
public sealed class RewriteKept
{
    /// <summary>Id of the finding.</summary>
    public required string FindingId { get; init; }

    /// <summary>Why the wording can stay (Czech).</summary>
    public string Reason { get; init; } = "";
}

/// <summary>
/// Tokens, price and counts of a rewrite.
/// </summary>
public sealed class RewriteStats
{
    /// <summary>Pages with findings to rewrite.</summary>
    public int Pages { get; init; }

    /// <summary>Pages taken from the cache.</summary>
    public int FromCache { get; init; }

    /// <summary>Pages the model could not rewrite.</summary>
    public int Errors { get; init; }

    /// <summary>Input tokens billed by OpenAI.</summary>
    public long InputTokens { get; init; }

    /// <summary>Of <see cref="InputTokens"/>, tokens read from the OpenAI prompt cache.</summary>
    public long CachedTokens { get; init; }

    /// <summary>Output tokens, reasoning included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Of <see cref="OutputTokens"/>, reasoning tokens.</summary>
    public long ReasoningTokens { get; init; }

    /// <summary>Price of the model in USD.</summary>
    public decimal CostUsd { get; init; }

    /// <summary>Jev requests of the check of the new passages.</summary>
    public int CheckCalls { get; init; }

    /// <summary>Price of the check by Jev in USD.</summary>
    public decimal CheckCostUsd { get; init; }

    /// <summary>Duration of the whole rewrite.</summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Progress of a rewrite.
/// </summary>
public sealed class RewriteProgress
{
    /// <summary>Pages done.</summary>
    public int Done { get; init; }

    /// <summary>Pages in total.</summary>
    public int Total { get; init; }
}
