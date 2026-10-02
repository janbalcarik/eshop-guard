using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EshopGuard.Application.Email;

/// <summary>
/// Composes an e-mail from the embedded templates (<c>Email/Templates/{locale}/</c>): the subject, the HTML body inside
/// <c>_layout.html</c> and the text body inside <c>_layout.txt</c>. Values are HTML-encoded in the HTML; labels (role names)
/// come from <c>labels.json</c> of the language. A missing template, parameter or placeholder fails (fail-closed), and a kind
/// with a token is refused when composed for the outbox.
/// </summary>
public sealed partial class EmailComposer
{
    private const string Prefix = "EshopGuard.Application.Email.Templates.";
    private static readonly Assembly Assembly = typeof(EmailComposer).Assembly;
    private readonly ConcurrentDictionary<string, string?> _files = new(StringComparer.Ordinal);

    /// <summary>Languages that have templates (folders of <c>Email/Templates</c>).</summary>
    public static IReadOnlyList<string> Locales { get; } = Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
        .Select(n => n[Prefix.Length..].Split('.')[0])
        .Distinct().Order(StringComparer.Ordinal).ToList();

    /// <summary>Composes an e-mail sent directly from a request (it may carry a token).</summary>
    public EmailMessage Compose(EmailTemplateKind kind, string locale, string to, IReadOnlyDictionary<string, object?> values) =>
        ComposeCore(kind, locale, to, values);

    /// <summary>Composes an e-mail of the outbox; a kind with a token is refused (<see cref="EmailTemplateException"/>).</summary>
    public EmailMessage ComposeFromOutbox(EmailTemplateKind kind, string locale, string to, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return kind.ContainsToken ? throw new EmailTemplateException("email.token_template_in_outbox", kind.Code) : ComposeCore(kind, locale, to, values);
    }

    /// <summary>Label of the language (e.g. <c>role.editor</c>), or <see cref="EmailTemplateException"/>.</summary>
    public string Label(string locale, string key)
    {
        var json = Read(locale, "labels.json") ?? throw new EmailTemplateException("email.template_missing", $"{locale}/labels.json");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(key, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new EmailTemplateException("email.label_missing", $"{locale}/{key}");
    }

    /// <summary>Text of an embedded template file, or <c>null</c>.</summary>
    public string? Read(string locale, string file) => _files.GetOrAdd(locale + "/" + file, _ =>
    {
        using var stream = Assembly.GetManifestResourceStream(Prefix + locale + "." + file);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    });

    /// <summary>Names of the placeholders and sections of a template text.</summary>
    public static IReadOnlySet<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Groups["name"].Value)
            .Concat(Section().Matches(text).Select(m => m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

    private EmailMessage ComposeCore(EmailTemplateKind kind, string locale, string to, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(values);
        var all = new Dictionary<string, object?>(values, StringComparer.Ordinal) { ["email"] = to };
        foreach (var name in kind.Parameters.Concat(kind.Flags))
        {
            if (!all.TryGetValue(name, out var value) || value is null)
            {
                throw new EmailTemplateException("email.parameter_missing", $"{kind.Code}/{name}");
            }
        }

        var subject = Render(kind, locale, "subject.txt", all, html: false).Trim();
        var html = Render(kind, locale, "html", all, html: true);
        var text = Render(kind, locale, "txt", all, html: false);
        var layoutValues = new Dictionary<string, object?>(StringComparer.Ordinal) { ["subject"] = subject, ["email"] = to };
        return new EmailMessage(
            to,
            subject,
            Fill(Template(locale, "_layout.html"), layoutValues, html: true, kind.Code, raw: ("body", html)),
            Fill(Template(locale, "_layout.txt"), layoutValues, html: false, kind.Code, raw: ("body", text)).Trim() + "\n",
            kind.Code,
            locale);
    }

    private string Render(EmailTemplateKind kind, string locale, string extension, Dictionary<string, object?> values, bool html)
    {
        var template = Template(locale, kind.Code + "." + extension);
        template = Section().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            if (!kind.Flags.Contains(name) || values[name] is not bool flag)
            {
                throw new EmailTemplateException("email.section_unknown", $"{locale}/{kind.Code}.{extension}/{name}");
            }

            return flag == (m.Groups["kind"].Value == "#") ? m.Groups["content"].Value : string.Empty;
        });
        return Fill(template, values, html, kind.Code + "." + extension, raw: null);
    }

    private string Template(string locale, string file) =>
        Read(locale, file) ?? throw new EmailTemplateException("email.template_missing", $"{locale}/{file}");

    private static string Fill(string template, IReadOnlyDictionary<string, object?> values, bool html, string where, (string Name, string Value)? raw) =>
        Placeholder().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            if (raw is { } body && name == body.Name)
            {
                return body.Value;
            }

            if (!values.TryGetValue(name, out var value) || value is null)
            {
                throw new EmailTemplateException("email.placeholder_unknown", $"{where}/{name}");
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return html ? HtmlEncoder.Default.Encode(text) : text;
        });

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z][A-Za-z0-9]*)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{\{(?<kind>[#^])(?<name>[A-Za-z][A-Za-z0-9]*)\}\}(?<content>.*?)\{\{/\k<name>\}\}", RegexOptions.Singleline)]
    private static partial Regex Section();
}

/// <summary>A template, parameter or label is missing, or a kind with a token was composed from the outbox; the message is a code and a place.</summary>
public sealed class EmailTemplateException(string code, string place) : Exception($"{code}: {place}")
{
    public string Code { get; } = code;

    public string Place { get; } = place;
}
