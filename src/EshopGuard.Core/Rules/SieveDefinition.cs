namespace EshopGuard.Core.Rules;

/// <summary>
/// The block sieve from <c>config/sieve.yaml</c>: one topic question per sentence module. Before the detailed questions,
/// every chunk of the main text of a page (consecutive text blocks) is asked whether the topic of the module appears in it;
/// the sentences of a chunk go to the detailed questions of a module only when the chunk reaches the threshold.
/// </summary>
public sealed class SieveDefinition
{
    /// <summary>Version of the sieve questions; part of the cache key of sieve answers, separate from the rule sets.</summary>
    public string Version { get; set; } = "";

    /// <summary>A chunk with at least this probability for a module sends its sentences to the module.</summary>
    public double Threshold { get; set; } = 0.2;

    /// <summary>Chunks are consecutive text blocks up to this length (a longer block is a chunk of its own).</summary>
    public int MaxChunkChars { get; set; } = 600;

    /// <summary>
    /// A chunk longer than this is never sent to the sieve and its sentences are evaluated in detail; far below the
    /// Jev limit of about 32 000 tokens per request (about 80 000 characters of Slovak text, measured 30. 9. 2026).
    /// </summary>
    public int MaxRequestChars { get; set; } = 20_000;

    /// <summary>Topic question per module, keyed by module name.</summary>
    public Dictionary<string, SieveQuestion> Questions { get; set; } = [];

    /// <summary>Id of the sieve question of a module in Jev requests.</summary>
    public static string QuestionId(string module) => "sieve_" + module;
}

/// <summary>
/// A topic question of the sieve.
/// </summary>
public sealed class SieveQuestion
{
    /// <summary>English text sent to Jev.</summary>
    public string TextEn { get; set; } = "";

    /// <summary>Czech text.</summary>
    public string TextCs { get; set; } = "";
}
