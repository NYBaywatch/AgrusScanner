using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgrusScanner.Models;

/// <summary>
/// The detection definitions: probes, AI port preset, Docker image patterns.
/// This is the payload inside a signature package and the embedded baseline.
/// </summary>
public class SignatureCatalog
{
    public const int CurrentSchemaVersion = 1;

    public static readonly string[] ValidCategories =
    [
        "LLM", "Image Gen", "Video Gen", "Voice / STT / TTS", "ML Platform",
        "AI Platform", "Agent Platform", "RAG Platform", "Vector DB",
        "Embeddings", "Container", "GPU Infra", "MCP Server"
    ];

    public static readonly string[] ValidConfidences = ["high", "medium", "low"];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public ProbeDefinition[] Probes { get; init; } = [];
    public int[] AiPorts { get; init; } = [];
    public string[] DockerPatterns { get; init; } = [];

    /// <summary>Version label of the package this catalog came from ("embedded" for the baseline).</summary>
    [JsonIgnore]
    public string SourceVersion { get; set; } = "embedded";

    public static SignatureCatalog Parse(byte[] json)
    {
        var catalog = JsonSerializer.Deserialize<SignatureCatalog>(json, JsonOptions)
            ?? throw new InvalidDataException("Catalog JSON is empty.");
        var errors = catalog.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException("Catalog failed validation:\n  " + string.Join("\n  ", errors));
        return catalog;
    }

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    /// <summary>
    /// Structural invariants. Mirrors ProbeCatalogTests so a bad feed can never be loaded at runtime.
    /// </summary>
    public List<string> Validate(int minimumProbeCount = 0)
    {
        var errors = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"schemaVersion {SchemaVersion} is not supported (expected {CurrentSchemaVersion}).");
        if (Probes.Length < minimumProbeCount)
            errors.Add($"Catalog has {Probes.Length} probes, below the required minimum of {minimumProbeCount}.");
        if (Probes.Length == 0) errors.Add("Catalog has no probes.");
        if (AiPorts.Length == 0) errors.Add("Catalog has no AI ports.");
        if (DockerPatterns.Length == 0) errors.Add("Catalog has no Docker patterns.");

        foreach (var p in Probes)
        {
            var id = $"'{p.ServiceName}' ({p.Path})";
            if (string.IsNullOrWhiteSpace(p.ServiceName)) errors.Add($"Probe with path '{p.Path}' has no serviceName.");
            if (!ValidCategories.Contains(p.Category)) errors.Add($"Probe {id} has invalid category '{p.Category}'.");
            if (!ValidConfidences.Contains(p.Confidence)) errors.Add($"Probe {id} has invalid confidence '{p.Confidence}'.");
            if (p.Specificity is < 1 or > 100) errors.Add($"Probe {id} has specificity {p.Specificity}, must be 1-100.");
            if (!p.Path.StartsWith('/')) errors.Add($"Probe {id} path is not rooted.");
            if (p.BodyContains is null && p.HeaderContains is null && p.StatusCode is null)
                errors.Add($"Probe {id} matches everything: it needs bodyContains, headerContains, or statusCode.");
            if (p.Method is not null && p.Method is not ("GET" or "POST"))
                errors.Add($"Probe {id} has unsupported method '{p.Method}'.");
            if (p.Body is not null && p.Method != "POST")
                errors.Add($"Probe {id} has a body but method is not POST.");
            if (p.Method == "POST" && (p.Body is null || p.ContentType is null))
                errors.Add($"Probe {id} is a POST but lacks body or contentType.");
            if (p.Headers is not null)
                foreach (var (hn, hv) in p.Headers)
                    if (hn.Length == 0 || hn.Any(ch => ch <= ' ' || ch > '~' || ch == ':') || hv.Any(ch => ch < ' ' || ch > '~'))
                        errors.Add($"Probe {id} has an invalid header '{hn}'.");
            if (p.PortHint is < 1 or > 65535) errors.Add($"Probe {id} has invalid portHint {p.PortHint}.");
        }

        foreach (var g in Probes.GroupBy(p => (p.Path, p.Method, p.BodyContains, p.HeaderContains, p.StatusCode, p.PortHint)).Where(g => g.Count() > 1))
            errors.Add($"Duplicate signature at {g.Key.Path}: [{string.Join(", ", g.Select(p => p.ServiceName))}].");

        foreach (var port in AiPorts.Where(x => x is < 1 or > 65535)) errors.Add($"Invalid AI port {port}.");
        if (AiPorts.Distinct().Count() != AiPorts.Length) errors.Add("Duplicate AI ports.");
        foreach (var d in DockerPatterns.Where(string.IsNullOrWhiteSpace)) errors.Add("Empty Docker pattern.");

        return errors;
    }
}
