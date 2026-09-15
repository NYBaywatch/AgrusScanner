namespace AgrusScanner.Models;

/// <summary>
/// One detection signature. Loaded from the signature catalog (signatures/catalog.json
/// embedded at build time, or a verified .agsig package installed at runtime).
/// Field names are the JSON contract — do not rename without bumping the catalog schemaVersion.
/// </summary>
public class ProbeDefinition
{
    public string Path { get; init; } = "/";
    public string ServiceName { get; init; } = "";
    public string Category { get; init; } = "";
    public string Confidence { get; init; } = "low";
    public int Specificity { get; init; }
    public int? StatusCode { get; init; }
    public string? BodyContains { get; init; }
    public string? HeaderContains { get; init; }
    public int? PortHint { get; init; } // only run this probe on this specific port

    // ── Request shaping (optional; default is a bare GET) ──
    // Reserved in schema v1 so POST-style services (e.g. MCP initialize) can be added by signature alone.
    public string? Method { get; init; }        // "GET" (default) or "POST"
    public string? AcceptHeader { get; init; }  // e.g. "application/json, text/event-stream"
    public string? ContentType { get; init; }   // e.g. "application/json"
    public string? Body { get; init; }          // request body for POST
    public Dictionary<string, string>? Headers { get; init; } // extra request headers, e.g. MCP-Protocol-Version

    /// <summary>Human note for catalog maintainers; ignored by the engine.</summary>
    public string? Note { get; init; }
}
