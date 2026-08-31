using AgrusScanner.Services;
using Xunit;

namespace AgrusScanner.Tests;

// Invariant gates for the AI detection probe catalog. These exist so automated
// signature updates cannot ship a malformed, duplicate, or shrunken catalog.
public class ProbeCatalogTests
{
    // Baseline at the time these gates were added. Signature updates are
    // additive-only; raise this floor when probes are intentionally added.
    private const int BaselineProbeCount = 99;

    private static readonly string[] ValidCategories =
    [
        "LLM", "Image Gen", "Video Gen", "Voice / STT / TTS", "ML Platform",
        "AI Platform", "Agent Platform", "RAG Platform", "Vector DB",
        "Embeddings", "Container", "GPU Infra", "MCP Server"
    ];

    private static readonly string[] ValidConfidences = ["high", "medium", "low"];

    [Fact]
    public void Catalog_never_shrinks_below_baseline()
    {
        Assert.True(AiServiceProber.Probes.Length >= BaselineProbeCount,
            $"Probe catalog has {AiServiceProber.Probes.Length} entries, below the baseline of {BaselineProbeCount}. Probes must not be removed.");
    }

    [Fact]
    public void Every_probe_has_service_name_and_category()
    {
        foreach (var probe in AiServiceProber.Probes)
        {
            Assert.False(string.IsNullOrWhiteSpace(probe.ServiceName),
                $"Probe with path '{probe.Path}' has no ServiceName.");
            Assert.Contains(probe.Category, ValidCategories);
        }
    }

    [Fact]
    public void Every_probe_has_valid_confidence_and_specificity()
    {
        foreach (var probe in AiServiceProber.Probes)
        {
            Assert.Contains(probe.Confidence, ValidConfidences);
            Assert.InRange(probe.Specificity, 1, 100);
        }
    }

    [Fact]
    public void Every_probe_path_is_rooted()
    {
        foreach (var probe in AiServiceProber.Probes)
        {
            Assert.StartsWith("/", probe.Path);
        }
    }

    [Fact]
    public void Every_probe_has_at_least_one_match_criterion()
    {
        foreach (var probe in AiServiceProber.Probes)
        {
            var hasCriterion = probe.BodyContains is not null
                || probe.HeaderContains is not null
                || probe.StatusCode is not null;
            Assert.True(hasCriterion,
                $"Probe '{probe.ServiceName}' ({probe.Path}) matches everything: it needs BodyContains, HeaderContains, or StatusCode.");
        }
    }

    [Fact]
    public void No_two_probes_share_an_identical_signature()
    {
        var duplicates = AiServiceProber.Probes
            .GroupBy(p => (p.Path, p.BodyContains, p.HeaderContains, p.StatusCode, p.PortHint))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Path} [{string.Join(", ", g.Select(p => p.ServiceName))}]")
            .ToList();

        Assert.True(duplicates.Count == 0,
            $"Duplicate probe signatures found: {string.Join("; ", duplicates)}");
    }
}
