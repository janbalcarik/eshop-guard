using System.Text;
using System.Text.RegularExpressions;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Simple robots.txt parser (RFC 9309 subset): User-agent, Allow, Disallow and Sitemap, plus the common Crawl-delay.
/// Rules of the group naming our product token apply, otherwise the <c>*</c> group.
/// The longest matching pattern wins and Allow wins a tie.
/// </summary>
internal sealed class RobotsTxt
{
    private readonly IReadOnlyList<Rule> _rules;

    private RobotsTxt(IReadOnlyList<Rule> rules, IReadOnlyList<string> sitemaps, TimeSpan? crawlDelay = null)
    {
        _rules = rules;
        Sitemaps = sitemaps;
        CrawlDelay = crawlDelay;
    }

    /// <summary>Crawl-delay of the applicable group (the longest when several groups apply); null when not given.</summary>
    public TimeSpan? CrawlDelay { get; }

    /// <summary>Used when robots.txt does not exist (4xx).</summary>
    public static RobotsTxt AllowAll { get; } = new([], []);

    /// <summary>Used when robots.txt is unreachable (5xx or network error), as RFC 9309 requires.</summary>
    public static RobotsTxt DisallowAll { get; } = new([Rule.Create(allow: false, "/")], []);

    /// <summary>Sitemap URLs listed in the file.</summary>
    public IReadOnlyList<string> Sitemaps { get; }

    public static RobotsTxt Parse(string content, string productToken)
    {
        var groups = new List<(List<string> Agents, List<Rule> Rules, List<double> Delays)>();
        var sitemaps = new List<string>();
        (List<string> Agents, List<Rule> Rules, List<double> Delays)? current = null;
        var lastWasAgent = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }

            line = line.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            switch (field)
            {
                case "user-agent":
                    if (!lastWasAgent || current is null)
                    {
                        current = ([], [], []);
                        groups.Add(current.Value);
                    }

                    current.Value.Agents.Add(value.ToLowerInvariant());
                    lastWasAgent = true;
                    break;
                case "allow":
                case "disallow":
                    lastWasAgent = false;
                    if (current is null || value.Length == 0)
                    {
                        // Rules before any User-agent are ignored; an empty Disallow allows everything.
                        break;
                    }

                    current.Value.Rules.Add(Rule.Create(field == "allow", value));
                    break;
                case "sitemap":
                    if (value.Length > 0)
                    {
                        sitemaps.Add(value);
                    }

                    break;
                case "crawl-delay":
                    lastWasAgent = false;
                    if (current is not null
                        && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                        && seconds > 0)
                    {
                        current.Value.Delays.Add(seconds);
                    }

                    break;
                default:
                    lastWasAgent = false;
                    break;
            }
        }

        var token = productToken.ToLowerInvariant();
        var matching = groups.Where(g => g.Agents.Contains(token)).ToList();
        if (matching.Count == 0)
        {
            matching = groups.Where(g => g.Agents.Contains("*")).ToList();
        }

        var delays = matching.SelectMany(g => g.Delays).ToList();
        return new RobotsTxt(matching.SelectMany(g => g.Rules).ToList(), sitemaps, delays.Count > 0 ? TimeSpan.FromSeconds(delays.Max()) : null);
    }

    /// <summary>Whether the URL may be downloaded.</summary>
    public bool IsAllowed(Uri url)
    {
        var path = Uri.UnescapeDataString(url.PathAndQuery);
        if (path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Rule? best = null;
        foreach (var rule in _rules)
        {
            if (!Matches(rule, path))
            {
                continue;
            }

            if (best is null || rule.Length > best.Length || (rule.Length == best.Length && rule.Allow && !best.Allow))
            {
                best = rule;
            }
        }

        return best is null || best.Allow;
    }

    /// <summary>A pattern that cannot be evaluated in time counts as matching, so the crawler rather skips the URL.</summary>
    private static bool Matches(Rule rule, string path)
    {
        try
        {
            return rule.Regex.IsMatch(path);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    private sealed record Rule(bool Allow, int Length, Regex Regex)
    {
        public static Rule Create(bool allow, string pattern)
        {
            var decoded = Uri.UnescapeDataString(pattern);
            var anchored = decoded.EndsWith('$');
            var body = anchored ? decoded[..^1] : decoded;
            var regex = new StringBuilder("^");
            foreach (var c in body)
            {
                regex.Append(c == '*' ? ".*" : Regex.Escape(c.ToString()));
            }

            if (anchored)
            {
                regex.Append('$');
            }

            // Patterns with several wildcards backtrack badly on long URLs; the non-backtracking engine runs in linear time.
            return new Rule(allow, decoded.Length, new Regex(regex.ToString(), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1)));
        }
    }
}
