using EshopGuard.Application.Email;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Ref;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EshopGuard.Application.Tests;

/// <summary>
/// Every kind of e-mail exists in every language of <c>ref.locales</c> with all three files and the same placeholders (change
/// 9, task 3.6; specification „Šablona chybí v jednom jazyce“): a missing file is named in the failure.
/// </summary>
public sealed class EmailTemplateCompletenessTests
{
    private static readonly string[] Files = ["subject.txt", "html", "txt"];
    private readonly EmailComposer _composer = new();

    /// <summary>Languages of the seed of <c>ref.locales</c> (all of them, enabled or not yet).</summary>
    public static IReadOnlyList<string> RequiredLocales()
    {
        var builder = new DbContextOptionsBuilder<EshopGuardDb>();
        builder.UseEshopGuardNpgsql("Host=localhost;Database=model_only");
        var options = builder.Options;
        using var db = new EshopGuardDb(options, new TenantContext());
        return db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(typeof(Locale))!.GetSeedData().Select(r => (string)r[nameof(Locale.Code)]!).Order(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void EveryKind_InEveryLanguage_HasAllThreeFiles_AndTheLayout()
    {
        var locales = RequiredLocales();
        Assert.Equal(["cs", "sk"], locales);
        var missing = new List<string>();
        foreach (var locale in locales)
        {
            missing.AddRange(new[] { "_layout.html", "_layout.txt", "labels.json" }
                .Where(f => _composer.Read(locale, f) is null).Select(f => $"Email/Templates/{locale}/{f}"));
            foreach (var kind in EmailTemplateKind.All)
            {
                missing.AddRange(Files.Where(f => _composer.Read(locale, kind.Code + "." + f) is null).Select(f => $"Email/Templates/{locale}/{kind.Code}.{f}"));
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryFile_UsesTheSamePlaceholdersInEveryLanguage_AndOnlyKnownOnes()
    {
        var locales = RequiredLocales();
        var problems = new List<string>();
        foreach (var kind in EmailTemplateKind.All)
        {
            var known = kind.Parameters.Concat(kind.Flags).ToHashSet(StringComparer.Ordinal);
            foreach (var file in Files)
            {
                var sets = locales.ToDictionary(l => l, l => EmailComposer.Placeholders(_composer.Read(l, kind.Code + "." + file) ?? string.Empty));
                var reference = sets[locales[0]];
                problems.AddRange(sets.Where(s => !s.Value.SetEquals(reference)).Select(s => $"{s.Key}/{kind.Code}.{file}: {string.Join(",", s.Value.Order())}"));
                problems.AddRange(reference.Where(p => !known.Contains(p)).Select(p => $"{kind.Code}.{file}: unknown {p}"));
            }

            if (kind.Parameters.Contains("link"))
            {
                problems.AddRange(locales.Where(l => !EmailComposer.Placeholders(_composer.Read(l, kind.Code + ".txt")!).Contains("link")).Select(l => $"{l}/{kind.Code}.txt: no link"));
            }
        }

        var labels = locales.Select(l => System.Text.Json.JsonDocument.Parse(_composer.Read(l, "labels.json")!).RootElement.EnumerateObject().Select(p => p.Name).Order().ToList()).ToList();
        Assert.All(labels, l => Assert.Equal(labels[0], l));
        Assert.Empty(problems);
    }

    [Fact]
    public void Composer_EncodesValuesInHtml_ButNotInText()
    {
        var message = _composer.Compose(EmailTemplateKind.Invitation, "sk", "peter@agentura.cz", new Dictionary<string, object?>
        {
            ["link"] = "https://app.eshopguard.test/pozvanka#t=abc",
            ["days"] = 7,
            ["tenantName"] = "<script>alert(1)</script> & spol.",
            ["inviterName"] = "Jana",
            ["roleName"] = "editor",
        });

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; spol.", message.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", message.Html, StringComparison.Ordinal);
        Assert.Contains("<script>alert(1)</script> & spol.", message.Text, StringComparison.Ordinal);
        Assert.Contains("Pozvánka do účtu", message.Subject, StringComparison.Ordinal);
        Assert.Contains("· pre peter@agentura.cz", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void OutboxRefusesKindsWithAToken_AndMissingParametersFail()
    {
        var values = new Dictionary<string, object?> { ["link"] = "x", ["minutes"] = 15, ["isNewAccount"] = false };

        var refused = Assert.Throws<EmailTemplateException>(() => _composer.ComposeFromOutbox(EmailTemplateKind.LoginLink, "sk", "a@b.sk", values));
        Assert.Equal("email.token_template_in_outbox", refused.Code);
        var missing = Assert.Throws<EmailTemplateException>(() => _composer.Compose(EmailTemplateKind.LoginLink, "sk", "a@b.sk", new Dictionary<string, object?> { ["link"] = "x" }));
        Assert.Equal("email.parameter_missing", missing.Code);
        var language = Assert.Throws<EmailTemplateException>(() => _composer.Compose(EmailTemplateKind.LoginLink, "de", "a@b.sk", values));
        Assert.Equal("email.template_missing", language.Code);
    }

    [Fact]
    public void NewAccountSection_ShowsOnlyForANewAccount()
    {
        string Text(bool isNew) => _composer.Compose(EmailTemplateKind.LoginLink, "sk", "a@b.sk",
            new Dictionary<string, object?> { ["link"] = "https://x/#t=1", ["minutes"] = 15, ["isNewAccount"] = isNew }).Text;

        Assert.Contains("Účet vytvoríme", Text(true), StringComparison.Ordinal);
        Assert.DoesNotContain("Účet vytvoríme", Text(false), StringComparison.Ordinal);
        Assert.DoesNotContain("{{", Text(false), StringComparison.Ordinal);
    }
}
