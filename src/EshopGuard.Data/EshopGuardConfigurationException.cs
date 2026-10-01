namespace EshopGuard.Data;

/// <summary>
/// Missing or invalid configuration. The message names only the code and the key, never a value.
/// </summary>
public sealed class EshopGuardConfigurationException(string code, string key)
    : Exception($"{code}: {key}")
{
    /// <summary>Machine-readable code, e.g. <c>config.connection_string_missing</c>.</summary>
    public string Code { get; } = code;

    /// <summary>Name of the configuration key (without its value).</summary>
    public string Key { get; } = key;
}
