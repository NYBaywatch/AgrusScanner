using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgrusScanner.Services;

/// <summary>
/// The .agsig envelope. Shared source: compiled into the app (verify/unpack) and into
/// AgrusScanner.SigTool (pack). No WPF dependencies allowed in this file.
///
///   magic "AGSG" | format u8 | headerLen u16 LE | header JSON | payload | signature (64 bytes)
///   payload   = AES-256-GCM( nonce(12) || ciphertext || tag(16) ) over gzip(catalog JSON)
///   signature = ECDSA P-256 / SHA-256, IEEE P1363 (r||s), over every byte before it
/// </summary>
public static class SignaturePackage
{
    public const byte FormatVersion = 1;
    public const int SignatureLength = 64;
    private static readonly byte[] Magic = "AGSG"u8.ToArray();
    private const int NonceLength = 12, TagLength = 16;
    private const int MaxInflatedBytes = 16 * 1024 * 1024;

    public sealed class Header
    {
        public string SigVersion { get; init; } = "";
        public string MinAppVersion { get; init; } = "0.0.0";
        public string Created { get; init; } = "";
        public string KeyId { get; init; } = "k1";
        public int PayloadLength { get; init; }
        public string PayloadSha256 { get; init; } = "";
    }

    private static readonly JsonSerializerOptions HeaderJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ───────────── pack (SigTool / tests) ─────────────

    public static byte[] Pack(byte[] catalogJson, string sigVersion, string minAppVersion, ECDsa privateKey, byte[] aesKey, string keyId = "k1")
    {
        // gzip
        using var gz = new MemoryStream();
        using (var z = new GZipStream(gz, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(catalogJson);
        var plain = gz.ToArray();

        // encrypt
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(aesKey, TagLength))
            aes.Encrypt(nonce, plain, cipher, tag);
        var payload = new byte[NonceLength + cipher.Length + TagLength];
        nonce.CopyTo(payload, 0);
        cipher.CopyTo(payload, NonceLength);
        tag.CopyTo(payload, NonceLength + cipher.Length);

        var header = new Header
        {
            SigVersion = sigVersion,
            MinAppVersion = minAppVersion,
            Created = DateTime.UtcNow.ToString("O"),
            KeyId = keyId,
            PayloadLength = payload.Length,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()
        };
        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header, HeaderJson);
        if (headerBytes.Length > ushort.MaxValue) throw new InvalidDataException("Header too large.");

        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.WriteByte(FormatVersion);
        ms.Write(BitConverter.GetBytes((ushort)headerBytes.Length));
        ms.Write(headerBytes);
        ms.Write(payload);

        var signature = privateKey.SignData(ms.ToArray(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (signature.Length != SignatureLength) throw new CryptographicException("Unexpected signature length.");
        ms.Write(signature);
        return ms.ToArray();
    }

    // ───────────── verify + unpack (app) ─────────────

    /// <summary>
    /// Verifies the package and returns the decoded catalog JSON. Throws <see cref="SignatureException"/>
    /// on ANY problem; callers must treat a throw as "discard this file".
    /// Signature is checked before anything else is parsed or decrypted.
    /// </summary>
    public static byte[] Unpack(ReadOnlySpan<byte> package, ECDsa publicKey, byte[] aesKey, out Header header)
    {
        const int fixedPrefix = 4 + 1 + 2;
        if (package.Length < fixedPrefix + SignatureLength) throw new SignatureException("File too small.");
        if (!package[..4].SequenceEqual(Magic)) throw new SignatureException("Not a signature package (bad magic).");
        if (package[4] != FormatVersion) throw new SignatureException($"Unsupported package format {package[4]}.");

        // 1. authenticity — before touching anything else
        var signed = package[..^SignatureLength];
        var signature = package[^SignatureLength..];
        if (!publicKey.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new SignatureException("Signature verification failed. The file was not produced by the Agrus build pipeline or has been modified.");

        // 2. header
        var headerLen = BitConverter.ToUInt16(package.Slice(5, 2));
        if (fixedPrefix + headerLen > signed.Length) throw new SignatureException("Header length out of range.");
        try
        {
            header = JsonSerializer.Deserialize<Header>(package.Slice(fixedPrefix, headerLen), HeaderJson)
                ?? throw new SignatureException("Empty header.");
        }
        catch (JsonException ex) { throw new SignatureException("Malformed header: " + ex.Message); }

        // 3. payload integrity
        var payload = signed[(fixedPrefix + headerLen)..];
        if (payload.Length != header.PayloadLength) throw new SignatureException("Payload length mismatch.");
        var sha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        if (!string.Equals(sha, header.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new SignatureException("Payload hash mismatch.");
        if (payload.Length < NonceLength + TagLength) throw new SignatureException("Payload too small.");

        // 4. decrypt + inflate
        var plain = new byte[payload.Length - NonceLength - TagLength];
        try
        {
            using var aes = new AesGcm(aesKey, TagLength);
            aes.Decrypt(payload[..NonceLength], payload[NonceLength..^TagLength], payload[^TagLength..], plain);
        }
        catch (CryptographicException) { throw new SignatureException("Payload decryption failed."); }

        try
        {
            using var z = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            var buf = new byte[64 * 1024];
            int n;
            while ((n = z.Read(buf, 0, buf.Length)) > 0)
            {
                outMs.Write(buf, 0, n);
                if (outMs.Length > MaxInflatedBytes) throw new SignatureException("Payload inflates beyond the allowed size.");
            }
            return outMs.ToArray();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        { throw new SignatureException("Payload is not valid gzip."); }
    }

    // ───────────── key helpers ─────────────

    public static ECDsa ImportPublicKey(string base64Spki)
    {
        var k = ECDsa.Create();
        k.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64Spki), out _);
        return k;
    }

    public static ECDsa ImportPrivateKey(string base64Pkcs8)
    {
        var k = ECDsa.Create();
        k.ImportPkcs8PrivateKey(Convert.FromBase64String(base64Pkcs8), out _);
        return k;
    }

    public static (string publicKey, string privateKey) GenerateKeyPair()
    {
        using var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(k.ExportSubjectPublicKeyInfo()),
                Convert.ToBase64String(k.ExportPkcs8PrivateKey()));
    }

    public static Version ParseVersion(string s)
    {
        var cleaned = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(cleaned, out var v) ? v : new Version(0, 0, 0);
    }

    /// <summary>Compares sigVersion strings like "2026.09.15.1" numerically, segment by segment.</summary>
    public static int CompareSigVersions(string a, string b)
    {
        var pa = a.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var c = (i < pa.Length ? pa[i] : 0).CompareTo(i < pb.Length ? pb[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }
}

public sealed class SignatureException(string message) : Exception(message);
