using System.Diagnostics;
using System.IO;
using System.Reflection;
using AgrusScanner.Models;

namespace AgrusScanner.Services;

/// <summary>
/// Owns the live <see cref="SignatureCatalog"/>.
///
/// Load order at startup:
///   1. Embedded baseline (signatures/catalog.json compiled into this assembly). Always loads.
///   2. %LocalAppData%\AgrusScanner\signatures.agsig, if present and it passes every check in
///      <see cref="TryInstall"/>. Otherwise it is ignored and the baseline stays active.
///
/// Nothing from outside the binary is ever used without passing <see cref="SignaturePackage.Unpack"/>
/// (ECDSA P-256 signature against the compiled-in public key) plus the version and catalog invariants.
/// </summary>
public static class SignatureStore
{
    // Public half of the CI signing key. Rotating it requires an app release (see spec, Key management).
    private static readonly Dictionary<string, string> PublicKeys = new()
    {
        ["k1"] = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEC5RpskDd66+WaISHNTfcW1wa2FPKf85ZX+ZbV7uF5JBXBGgx8sRMAeB7o5+Q9f9GeiQc0L+CweJz6Rv98fqRFQ=="
    };

    // Payload obfuscation key. Not a secret (it ships in the binary); the signature is the trust boundary.
    private static readonly byte[] AesKey = Convert.FromBase64String("/H5xTLCWR6TV2nzOhxeAMyrgvAuo5v6KeFo93gqPJhA=");

    private const string EmbeddedResourceName = "AgrusScanner.signatures.catalog.json";
    public const long MaxPackageBytes = 4 * 1024 * 1024;

    public static readonly string InstalledPackagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgrusScanner", "signatures.agsig");

    private static readonly object Gate = new();
    private static SignatureCatalog? _current;
    private static SignatureCatalog? _embedded;

    /// <summary>The active catalog. Loads the embedded baseline on first access.</summary>
    public static SignatureCatalog Current
    {
        get
        {
            if (_current is not null) return _current;
            lock (Gate)
            {
                if (_current is null)
                {
                    _embedded = LoadEmbedded();
                    _current = _embedded;
                }
                return _current;
            }
        }
    }

    public static SignatureCatalog Embedded { get { _ = Current; return _embedded!; } }

    /// <summary>Raised on a background thread after a new catalog becomes active.</summary>
    public static event Action<SignatureCatalog>? CatalogChanged;

    /// <summary>Called once at startup: baseline, then the installed package if any.</summary>
    public static void Initialize()
    {
        _ = Current;
        if (!File.Exists(InstalledPackagePath)) return;
        try
        {
            if (new FileInfo(InstalledPackagePath).Length > MaxPackageBytes)
            {
                Debug.WriteLine("[SignatureStore] Ignoring installed package: file too large");
                return;
            }
            if (!TryInstall(File.ReadAllBytes(InstalledPackagePath), out var error, persist: false))
                Debug.WriteLine($"[SignatureStore] Ignoring installed package: {error}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SignatureStore] Could not read installed package: {ex.Message}");
        }
    }

    /// <summary>
    /// Verify a package and, if it passes every check, make it the active catalog. Returns false
    /// (with a reason) on any failure; the current catalog is left untouched in that case.
    /// </summary>
    public static bool TryInstall(byte[] package, out string error, bool persist = true)
    {
        error = "";
        try
        {
            if (package.Length > MaxPackageBytes) throw new SignatureException("Package too large.");

            SignatureCatalog catalog;
            SignaturePackage.Header header;
            // Verify and swap under one lock so two overlapping installs cannot race the no-downgrade rule.
            lock (Gate)
            {
                catalog = Verify(package, out header);
                if (persist)
                {
                    // Persist first: if the write fails, nothing changes in memory and the user sees the error.
                    Directory.CreateDirectory(Path.GetDirectoryName(InstalledPackagePath)!);
                    var tmp = InstalledPackagePath + ".tmp";
                    File.WriteAllBytes(tmp, package);
                    File.Move(tmp, InstalledPackagePath, overwrite: true);
                }
                _current = catalog;
            }
            Debug.WriteLine($"[SignatureStore] Active signatures: {header.SigVersion} ({catalog.Probes.Length} probes)");
            CatalogChanged?.Invoke(catalog);
            return true;
        }
        catch (Exception ex)
        {
            // Contract: never throw. A malformed-but-signed catalog is a CI bug, not a reason to crash the app.
            error = ex is SignatureException or InvalidDataException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>All load rules from the spec, in order. Throws on any failure.</summary>
    internal static SignatureCatalog Verify(byte[] package, out SignaturePackage.Header header)
    {
        // Rules 1-2 (magic, signature) and 5 (hash, decrypt, inflate) happen inside Unpack.
        // Key lookup: peek keyId only after the signature is verified against each known key.
        byte[]? json = null;
        SignaturePackage.Header? hdr = null;
        SignatureException? last = null;
        foreach (var (_, pub) in PublicKeys)
        {
            try
            {
                using var key = SignaturePackage.ImportPublicKey(pub);
                json = SignaturePackage.Unpack(package, key, AesKey, out hdr);
                break;
            }
            catch (SignatureException ex) { last = ex; }
        }
        if (json is null || hdr is null) throw last ?? new SignatureException("No signing key accepted the package.");
        header = hdr;

        // Rule 3: minimum app version
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        var minApp = SignaturePackage.ParseVersion(header.MinAppVersion);
        if (minApp > appVersion)
            throw new SignatureException($"Package requires Agrus Scanner {minApp} or newer (running {appVersion.ToString(3)}).");

        if (string.IsNullOrWhiteSpace(header.SigVersion)) throw new SignatureException("Package has no sigVersion.");

        // Rule 4: no downgrade below what is already active. The embedded baseline has no version and never
        // blocks, so at startup an older *signed* on-disk package is accepted; the next feed check replaces it.
        var active = Current;
        if (active.SourceVersion != "embedded" && SignaturePackage.CompareSigVersions(header.SigVersion, active.SourceVersion) <= 0)
            throw new SignatureException($"Package {header.SigVersion} is not newer than active signatures {active.SourceVersion}.");

        // Rule 6: catalog invariants, never shrink below the embedded baseline
        var catalog = SignatureCatalog.Parse(json);
        var errors = catalog.Validate(minimumProbeCount: Embedded.Probes.Length);
        if (errors.Count > 0) throw new SignatureException("Catalog failed validation: " + string.Join("; ", errors));

        catalog.SourceVersion = header.SigVersion;
        return catalog;
    }

    private static SignatureCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' is missing. Is signatures/catalog.json included in the build?");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var catalog = SignatureCatalog.Parse(ms.ToArray()); // build is broken if this throws
        catalog.SourceVersion = "embedded";
        return catalog;
    }
}
