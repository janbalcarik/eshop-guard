using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Published version of a rule set (catalogue from YAML). Table <c>checks.rule_sets</c>.</summary>
public sealed class RuleSet : GlobalEntity
{
    public required string Module { get; set; }

    public required string Version { get; set; }

    public string[] Jurisdictions { get; set; } = [];

    public required string QuestionLanguage { get; set; }

    public required byte[] QuestionSetHash { get; set; }

    public required JsonDocument Definition { get; set; }

    public required JsonDocument Texts { get; set; }

    public required byte[] SourceHash { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
