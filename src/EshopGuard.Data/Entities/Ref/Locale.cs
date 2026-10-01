using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ref;

/// <summary>Language of the user interface; only a language with complete texts can be enabled. Table <c>ref.locales</c>.</summary>
public sealed class Locale : IHasTimestamps
{
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? FallbackCode { get; set; }

    public bool Enabled { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
