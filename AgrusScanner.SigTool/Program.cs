using AgrusScanner.Models;
using AgrusScanner.Services;

// AgrusScanner.SigTool — builds, verifies, and dumps .agsig signature packages.
//
//   keygen                                   print a new ECDSA P-256 key pair (base64)
//   validate <catalog.json>                  run catalog invariants, exit 1 on failure
//   pack <catalog.json> --version V --min-app V --key <base64 pkcs8 | env:NAME> --aes <base64 | env:NAME> --out file.agsig
//   verify <file.agsig> --pub <base64 spki> --aes <base64 | env:NAME>
//   dump <file.agsig> --pub <base64 spki> --aes <base64 | env:NAME>     write decoded catalog JSON to stdout

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0) return Usage();
    var opts = ParseOptions(args.Skip(2));
    try
    {
        switch (args[0])
        {
            case "keygen":
            {
                var (pub, priv) = SignaturePackage.GenerateKeyPair();
                Console.WriteLine("PUBLIC  (embed in app):        " + pub);
                Console.WriteLine("PRIVATE (GitHub secret only):  " + priv);
                Console.WriteLine("AES     (embed in app + CI):   " + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
                return 0;
            }
            case "validate":
            {
                var catalog = SignatureCatalog.Parse(File.ReadAllBytes(Req(args, 1)));
                Console.WriteLine($"OK: {catalog.Probes.Length} probes, {catalog.AiPorts.Length} AI ports, {catalog.DockerPatterns.Length} Docker patterns.");
                return 0;
            }
            case "pack":
            {
                var catalogJson = File.ReadAllBytes(Req(args, 1));
                var catalog = SignatureCatalog.Parse(catalogJson); // validate before signing
                using var key = SignaturePackage.ImportPrivateKey(Secret(opts, "key"));
                var package = SignaturePackage.Pack(catalog.ToJson(), Opt(opts, "version"), opts.GetValueOrDefault("min-app", "0.0.0"),
                    key, Convert.FromBase64String(Secret(opts, "aes")), opts.GetValueOrDefault("key-id", "k1"));
                File.WriteAllBytes(Opt(opts, "out"), package);
                Console.WriteLine($"Wrote {Opt(opts, "out")} ({package.Length} bytes), sigVersion {Opt(opts, "version")}, {catalog.Probes.Length} probes.");
                return 0;
            }
            case "verify":
            case "dump":
            {
                using var pub = SignaturePackage.ImportPublicKey(Opt(opts, "pub"));
                var json = SignaturePackage.Unpack(File.ReadAllBytes(Req(args, 1)), pub, Convert.FromBase64String(Secret(opts, "aes")), out var header);
                var catalog = SignatureCatalog.Parse(json);
                if (args[0] == "dump") { Console.Write(System.Text.Encoding.UTF8.GetString(catalog.ToJson())); return 0; }
                Console.WriteLine($"OK: sigVersion {header.SigVersion}, minApp {header.MinAppVersion}, created {header.Created}, keyId {header.KeyId}, {catalog.Probes.Length} probes.");
                return 0;
            }
            default: return Usage();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("ERROR: " + ex.Message);
        return 1;
    }
}

static int Usage()
{
    Console.Error.WriteLine("usage: sigtool keygen | validate <catalog.json> | pack <catalog.json> --version V [--min-app V] --key K --aes A --out F | verify <f.agsig> --pub P --aes A | dump <f.agsig> --pub P --aes A");
    return 2;
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> a)
{
    var d = new Dictionary<string, string>();
    var list = a.ToList();
    for (var i = 0; i < list.Count; i++)
        if (list[i].StartsWith("--") && i + 1 < list.Count) d[list[i][2..]] = list[++i];
    return d;
}

static string Req(string[] args, int i) => args.Length > i ? args[i] : throw new ArgumentException("missing file argument");
static string Opt(Dictionary<string, string> o, string k) => o.TryGetValue(k, out var v) ? v : throw new ArgumentException($"--{k} is required");

// Secrets may be given inline or as env:NAME so they never appear on a CI command line.
static string Secret(Dictionary<string, string> o, string k)
{
    var v = Opt(o, k);
    if (!v.StartsWith("env:")) return v;
    return Environment.GetEnvironmentVariable(v[4..]) ?? throw new ArgumentException($"environment variable {v[4..]} is not set");
}
