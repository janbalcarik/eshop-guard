using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EshopGuard.Core.Pipeline;

/// <summary>Version of the serialized inputs and outputs of the pipeline steps.</summary>
internal static class PipelineSchema
{
    /// <summary>Current version; a record with another version is refused (<see cref="PipelineSchemaException"/>).</summary>
    public const int Version = 1;
}

/// <summary>A root record of a step (input or output) that is stored between jobs.</summary>
internal interface IPipelineRecord
{
    /// <summary>Version of the schema the record was written with.</summary>
    int SchemaVersion { get; }
}

/// <summary>A stored record has a schema version this code does not know.</summary>
internal sealed class PipelineSchemaException(string type, int version)
    : InvalidOperationException($"pipeline.schema_version: {type} has version {version}, this code reads {PipelineSchema.Version}.")
{
    public int Version { get; } = version;
}

/// <summary>JSON of the pipeline records: snake_case names and enum values, readable diacritics.</summary>
internal static class PipelineJson
{
    public static readonly JsonSerializerOptions Options = Create();

    public static string Serialize<T>(T record) => JsonSerializer.Serialize(record, Options);

    /// <summary>Reads a record and checks its schema version.</summary>
    public static T Deserialize<T>(string json)
        where T : IPipelineRecord
    {
        var record = JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Empty {typeof(T).Name}.");
        return record.SchemaVersion == PipelineSchema.Version ? record : throw new PipelineSchemaException(typeof(T).Name, record.SchemaVersion);
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = null,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
