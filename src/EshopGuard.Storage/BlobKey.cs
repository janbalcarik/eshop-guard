using System.Text.RegularExpressions;

namespace EshopGuard.Storage;

/// <summary>
/// Validated key of a stored file. Tenant files live under <c>tenants/{tenantId}/</c>, e-shop files under
/// <c>tenants/{tenantId}/shops/{shopId}/</c>. Every part must match <c>^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$</c>,
/// so <c>..</c>, empty parts, slashes inside a part and absolute paths are impossible.
/// </summary>
public sealed partial class BlobKey : IEquatable<BlobKey>
{
    /// <summary>Longest allowed key (fits common object stores such as S3 and Azure Blob Storage; parts are ASCII).</summary>
    public const int MaxLength = 1024;

    private BlobKey(string value, bool isPrefix)
    {
        Value = value;
        IsPrefix = isPrefix;
    }

    /// <summary>The key, e.g. <c>tenants/…/shops/…/pages/p1.html.gz</c>; prefixes end with <c>/</c>.</summary>
    public string Value { get; }

    /// <summary>True for a prefix (a "folder" for <see cref="IBlobStore.DeletePrefixAsync"/>), false for a file.</summary>
    public bool IsPrefix { get; }

    /// <summary>File of an e-shop: <c>tenants/{tenantId}/shops/{shopId}/{segments…}</c>.</summary>
    public static BlobKey ForShop(Guid tenantId, Guid shopId, params string[] segments)
    {
        RequireId(tenantId, nameof(tenantId));
        RequireId(shopId, nameof(shopId));
        RequireSegments(segments);
        return Create(["tenants", tenantId.ToString("D"), "shops", shopId.ToString("D"), .. segments], isPrefix: false);
    }

    /// <summary>File of a tenant outside any e-shop: <c>tenants/{tenantId}/{segments…}</c>.</summary>
    public static BlobKey ForTenant(Guid tenantId, params string[] segments)
    {
        RequireId(tenantId, nameof(tenantId));
        RequireSegments(segments);
        return Create(["tenants", tenantId.ToString("D"), .. segments], isPrefix: false);
    }

    /// <summary>Prefix of all files of a tenant (<c>tenants/{tenantId}/</c>) or of one e-shop.</summary>
    public static BlobKey Prefix(Guid tenantId, Guid? shopId = null)
    {
        RequireId(tenantId, nameof(tenantId));
        if (shopId is { } shop)
        {
            RequireId(shop, nameof(shopId));
            return Create(["tenants", tenantId.ToString("D"), "shops", shop.ToString("D")], isPrefix: true);
        }

        return Create(["tenants", tenantId.ToString("D")], isPrefix: true);
    }

    /// <summary>System file outside tenants (only the storage health probe).</summary>
    internal static BlobKey System(params string[] segments)
    {
        RequireSegments(segments);
        return Create(segments, isPrefix: false);
    }

    /// <summary>Path parts of the key.</summary>
    public IReadOnlyList<string> Segments => Value.TrimEnd('/').Split('/');

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    public bool Equals(BlobKey? other) => other is not null && other.IsPrefix == IsPrefix && string.Equals(other.Value, Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as BlobKey);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    private static BlobKey Create(string[] parts, bool isPrefix)
    {
        foreach (var part in parts)
        {
            if (part is null || part is "." or ".." || !SegmentPattern().IsMatch(part))
            {
                throw new ArgumentException("Invalid blob key part (allowed: letters, digits, '.', '_', '-'; must start with a letter or digit).", nameof(parts));
            }
        }

        var value = string.Join('/', parts) + (isPrefix ? "/" : string.Empty);
        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"Blob key longer than {MaxLength} characters.", nameof(parts));
        }

        return new BlobKey(value, isPrefix);
    }

    private static void RequireSegments(string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0)
        {
            throw new ArgumentException("A file key needs at least one part after the tenant or shop.", nameof(segments));
        }
    }

    private static void RequireId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Empty identifier.", name);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
