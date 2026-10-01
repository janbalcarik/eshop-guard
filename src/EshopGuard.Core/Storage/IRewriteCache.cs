namespace EshopGuard.Core.Storage;

/// <summary>
/// Cache of rewrites: the same page with the same findings, model and prompt is never paid for twice.
/// </summary>
public interface IRewriteCache
{
    /// <summary>Returns the cached answer (JSON and model), or null.</summary>
    Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stores an answer.</summary>
    Task SetAsync(string key, string json, string? model, CancellationToken ct = default);
}
