using System.IO;
using System.Security.Cryptography;
using AgrusScanner.Models;
using AgrusScanner.Services;
using Xunit;

namespace AgrusScanner.Tests;

// The .agsig envelope must reject anything not produced by our key, and anything altered after signing.
// These tests use a throwaway key pair; the real public key is only tested indirectly (SignatureStore).
public class SignaturePackageTests
{
    private static readonly (string Pub, string Priv) Keys = SignaturePackage.GenerateKeyPair();
    private static readonly byte[] Aes = RandomNumberGenerator.GetBytes(32);

    private static byte[] Baseline() => SignatureStore.Embedded.ToJson();

    private static byte[] Pack(byte[]? json = null, string version = "2026.09.15.1", string minApp = "0.0.0", string? priv = null)
    {
        using var key = SignaturePackage.ImportPrivateKey(priv ?? Keys.Priv);
        return SignaturePackage.Pack(json ?? Baseline(), version, minApp, key, Aes);
    }

    private static byte[] Unpack(byte[] package, string? pub = null, byte[]? aes = null)
    {
        using var key = SignaturePackage.ImportPublicKey(pub ?? Keys.Pub);
        return SignaturePackage.Unpack(package, key, aes ?? Aes, out _);
    }

    [Fact]
    public void Round_trip_returns_identical_catalog()
    {
        var json = Unpack(Pack());
        var catalog = SignatureCatalog.Parse(json);
        Assert.Equal(SignatureStore.Embedded.Probes.Length, catalog.Probes.Length);
        Assert.Equal(SignatureStore.Embedded.AiPorts, catalog.AiPorts);
        Assert.Equal(SignatureStore.Embedded.DockerPatterns, catalog.DockerPatterns);
    }

    [Fact]
    public void Header_carries_versions()
    {
        using var key = SignaturePackage.ImportPublicKey(Keys.Pub);
        SignaturePackage.Unpack(Pack(version: "2030.01.02.3", minApp: "0.4.0"), key, Aes, out var header);
        Assert.Equal("2030.01.02.3", header.SigVersion);
        Assert.Equal("0.4.0", header.MinAppVersion);
    }

    [Theory]
    [InlineData(10)]   // inside header
    [InlineData(-100)] // inside payload
    [InlineData(-1)]   // inside signature
    public void Flipping_any_byte_is_rejected(int offset)
    {
        var pkg = Pack();
        var i = offset >= 0 ? offset : pkg.Length + offset;
        pkg[i] ^= 0xFF;
        Assert.Throws<SignatureException>(() => Unpack(pkg));
    }

    [Fact]
    public void Package_signed_with_another_key_is_rejected()
    {
        var other = SignaturePackage.GenerateKeyPair();
        var pkg = Pack(priv: other.privateKey);
        var ex = Assert.Throws<SignatureException>(() => Unpack(pkg));
        Assert.Contains("Signature verification failed", ex.Message);
    }

    [Fact]
    public void Wrong_aes_key_is_rejected()
    {
        Assert.Throws<SignatureException>(() => Unpack(Pack(), aes: RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void Plain_json_is_rejected()
    {
        Assert.Throws<SignatureException>(() => Unpack(Baseline()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(70)]
    public void Truncated_or_empty_file_is_rejected(int length)
    {
        Assert.Throws<SignatureException>(() => Unpack(new byte[length]));
    }

    [Fact]
    public void Truncated_valid_package_is_rejected()
    {
        var pkg = Pack();
        Assert.Throws<SignatureException>(() => Unpack(pkg[..^1]));
    }

    [Fact]
    public void Catalog_with_duplicate_probe_fails_validation()
    {
        var baseline = SignatureStore.Embedded;
        var dup = new SignatureCatalog
        {
            Probes = [.. baseline.Probes, baseline.Probes[0]],
            AiPorts = baseline.AiPorts,
            DockerPatterns = baseline.DockerPatterns
        };
        Assert.Contains(dup.Validate(), e => e.StartsWith("Duplicate signature"));
    }

    [Fact]
    public void Catalog_smaller_than_minimum_fails_validation()
    {
        var baseline = SignatureStore.Embedded;
        var small = new SignatureCatalog { Probes = baseline.Probes[..5], AiPorts = baseline.AiPorts, DockerPatterns = baseline.DockerPatterns };
        Assert.Contains(small.Validate(minimumProbeCount: baseline.Probes.Length), e => e.Contains("below the required minimum"));
    }

    [Fact]
    public void Sig_version_comparison_is_numeric()
    {
        Assert.True(SignaturePackage.CompareSigVersions("2026.09.15.10", "2026.09.15.9") > 0);
        Assert.True(SignaturePackage.CompareSigVersions("2026.10.1.1", "2026.9.30.1") > 0);
        Assert.Equal(0, SignaturePackage.CompareSigVersions("1.2.3", "1.2.3.0"));
    }
}

// SignatureStore is the app's trust boundary: it must refuse anything not signed with the real key.
public class SignatureStoreTests
{
    [Fact]
    public void Embedded_baseline_loads_and_validates()
    {
        var c = SignatureStore.Embedded;
        Assert.Equal("embedded", c.SourceVersion);
        Assert.Empty(c.Validate());
        Assert.Equal(c.Probes.Length, AiServiceProber.Probes.Length);
        Assert.Equal(c.AiPorts, ScanConfig.AiPorts);
    }

    [Fact]
    public void Package_signed_with_unknown_key_is_not_installed()
    {
        var keys = SignaturePackage.GenerateKeyPair();
        using var priv = SignaturePackage.ImportPrivateKey(keys.privateKey);
        var pkg = SignaturePackage.Pack(SignatureStore.Embedded.ToJson(), "2099.1.1.1", "0.0.0", priv, RandomNumberGenerator.GetBytes(32));

        Assert.False(SignatureStore.TryInstall(pkg, out var error, persist: false));
        Assert.Contains("Signature verification failed", error);
        Assert.Equal("embedded", SignatureStore.Current.SourceVersion);
    }

    [Fact]
    public void Garbage_is_not_installed()
    {
        Assert.False(SignatureStore.TryInstall(new byte[] { 1, 2, 3 }, out _, persist: false));
        Assert.False(SignatureStore.TryInstall(SignatureStore.Embedded.ToJson(), out _, persist: false));
        Assert.Equal("embedded", SignatureStore.Current.SourceVersion);
    }
}

// Regression tests for the pre-1.0 security review: a signed-but-broken package must never throw out of TryInstall.
public class SignatureHardeningTests
{
    private static readonly (string Pub, string Priv) Keys = SignaturePackage.GenerateKeyPair();
    private static readonly byte[] Aes = RandomNumberGenerator.GetBytes(32);

    private static byte[] Pack(byte[] payload)
    {
        using var key = SignaturePackage.ImportPrivateKey(Keys.Priv);
        return SignaturePackage.Pack(payload, "2099.1.1.1", "0.0.0", key, Aes);
    }

    private static byte[] Unpack(byte[] pkg)
    {
        using var key = SignaturePackage.ImportPublicKey(Keys.Pub);
        return SignaturePackage.Unpack(pkg, key, Aes, out _);
    }

    [Fact]
    public void Signed_non_json_payload_is_rejected_by_parse_not_crash()
    {
        var json = Unpack(Pack("this is not json"u8.ToArray()));
        Assert.ThrowsAny<Exception>(() => SignatureCatalog.Parse(json));
        // and the store contract: false, never throw (uses the real key, so it fails at the signature step, still no throw)
        Assert.False(SignatureStore.TryInstall(Pack("this is not json"u8.ToArray()), out _, persist: false));
    }

    [Fact]
    public void Null_path_or_null_header_value_is_a_validation_error_not_an_exception()
    {
        var json = "{\"schemaVersion\":1,\"probes\":[{\"path\":null,\"serviceName\":\"X\",\"category\":\"LLM\",\"confidence\":\"high\",\"specificity\":50,\"statusCode\":200},"
                 + "{\"path\":\"/h\",\"serviceName\":\"Y\",\"category\":\"MCP Server\",\"confidence\":\"high\",\"specificity\":50,\"statusCode\":200,\"headers\":{\"A\":null}}],"
                 + "\"aiPorts\":[1],\"dockerPatterns\":[\"a\"]}";
        var ex = Assert.Throws<InvalidDataException>(() => SignatureCatalog.Parse(System.Text.Encoding.UTF8.GetBytes(json)));
        Assert.Contains("null path", ex.Message);
        Assert.Contains("invalid header", ex.Message);
    }

    [Fact]
    public void Post_is_only_allowed_for_mcp_discovery()
    {
        var baseline = SignatureStore.Embedded;
        var evil = new SignatureCatalog
        {
            Probes = [.. baseline.Probes, new ProbeDefinition
            {
                Path = "/admin/reset", Method = "POST", ServiceName = "Evil", Category = "LLM", Confidence = "high",
                Specificity = 50, ContentType = "application/json", Body = "{\"reset\":true}", StatusCode = 200
            }],
            AiPorts = baseline.AiPorts, DockerPatterns = baseline.DockerPatterns
        };
        var errors = evil.Validate();
        Assert.Contains(errors, e => e.Contains("POST outside the MCP Server category"));
        Assert.Contains(errors, e => e.Contains("not a JSON-RPC"));
    }

    [Fact]
    public void Shipped_catalog_post_bodies_pass_the_mcp_restriction()
    {
        Assert.Empty(SignatureStore.Embedded.Validate());
        Assert.Contains(SignatureStore.Embedded.Probes, p => p.Method == "POST");
    }

    [Fact]
    public void Gzip_bomb_is_rejected_at_inflate()
    {
        var bomb = new byte[20 * 1024 * 1024]; // zeros compress ~1000:1
        var ex = Assert.Throws<SignatureException>(() => Unpack(Pack(bomb)));
        Assert.Contains("inflates beyond", ex.Message);
    }

    [Fact]
    public void Oversized_package_is_rejected_before_verification()
    {
        Assert.False(SignatureStore.TryInstall(new byte[SignatureStore.MaxPackageBytes + 1], out var error, persist: false));
        Assert.Contains("too large", error);
    }
}
