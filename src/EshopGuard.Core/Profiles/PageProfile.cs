using System.Text.Json.Serialization;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Profile of one page template of a shop: the regions of the page, each with a CSS selector and whether its text is
/// checked or skipped. Written by a language model from outlines of sample pages and stored, so that later scans use the
/// same profile and differences between scans come from the shop, not from the model. Only "skip" regions change
/// anything; text outside every region is checked as before.
/// </summary>
public sealed class PageProfile
{
    /// <summary>Site and number, for example <c>vegis.sk#1</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Host without "www." (and the port when it is not the default).</summary>
    public required string Site { get; init; }

    /// <summary>When the profile was written.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Model that wrote it.</summary>
    public string? Model { get; init; }

    /// <summary>Version of the instructions the model got.</summary>
    public string PromptVersion { get; init; } = "";

    /// <summary>Pages whose outlines the model saw.</summary>
    public IReadOnlyList<string> SampleUrls { get; init; } = [];

    /// <summary>Regions as the model returned them; rejected ones keep the reason in <see cref="ProfileRegion.RejectedBecause"/>.</summary>
    public IReadOnlyList<ProfileRegion> Regions { get; init; } = [];

    /// <summary>Regions in use.</summary>
    [JsonIgnore]
    public IEnumerable<ProfileRegion> ActiveRegions => Regions.Where(r => r.RejectedBecause is null);
}

/// <summary>
/// One region of a template.
/// </summary>
public sealed class ProfileRegion
{
    /// <summary>Text of the region is checked.</summary>
    public const string Check = "check";

    /// <summary>Text of the region is not sent to Jev.</summary>
    public const string Skip = "skip";

    /// <summary>
    /// Roles that may be skipped: navigation, listings of other products and interface. Any other role is always checked,
    /// whatever the model says.
    /// </summary>
    public static readonly IReadOnlySet<string> SkippableRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        "navigation", "breadcrumb", "pagination", "filters", "related_products", "product_listing", "cookie_bar",
        "login_form", "newsletter_form", "comment_form", "search", "cart", "social_share",
    };

    /// <summary>What the region is, for example <c>main_description</c>, <c>badges</c> or <c>cookie_bar</c>.</summary>
    public required string Role { get; init; }

    /// <summary><see cref="Check"/> or <see cref="Skip"/>.</summary>
    public required string Action { get; init; }

    /// <summary>CSS selector of the region.</summary>
    public required string Selector { get; init; }

    /// <summary>Short text from the outline the model gave as an example.</summary>
    public string Example { get; init; } = "";

    /// <summary>Why the model chose the action.</summary>
    public string Reason { get; init; } = "";

    /// <summary>Why the region is not used (invalid selector, a skip region with checked text inside), or null.</summary>
    public string? RejectedBecause { get; init; }

    /// <summary>A copy of the region that is not used.</summary>
    public ProfileRegion Rejected(string because) => new()
    {
        Role = Role,
        Action = Action,
        Selector = Selector,
        Example = Example,
        Reason = Reason,
        RejectedBecause = because,
    };
}

/// <summary>
/// How one profile was used in a scan.
/// </summary>
public sealed class ProfileUse
{
    /// <summary>The profile.</summary>
    public required PageProfile Profile { get; init; }

    /// <summary>True when the profile was written in this scan.</summary>
    public bool CreatedInThisScan { get; init; }

    /// <summary>Pages that used the profile.</summary>
    public int Pages { get; set; }

    /// <summary>Characters left out on those pages, by role of the skip region.</summary>
    public Dictionary<string, long> SkippedCharsByRole { get; } = [];
}
