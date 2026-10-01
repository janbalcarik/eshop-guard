using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Report;

/// <summary>
/// Texts for the report writers: the renderer over the rules loaded once, and the language of the outputs
/// (<c>report.locale</c>, in the CLI the option <c>--lang</c>).
/// </summary>
internal sealed class ReportTexts(IRuleSetProvider rules, IOptions<EshopGuardOptions> options)
{
    private readonly Lazy<Task<RuleTextRenderer>> _renderer = new(async () => new RuleTextRenderer(await rules.LoadAsync()));

    /// <summary>Language of the texts in the outputs.</summary>
    public string Locale => options.Value.Report.Locale;

    /// <summary>The renderer over the texts of the rules.</summary>
    public Task<RuleTextRenderer> RendererAsync() => _renderer.Value;
}
