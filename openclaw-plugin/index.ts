/**
 * Agrus Scanner — OpenClaw Plugin
 *
 * Exposes network scanning and AI/ML service detection tools.
 * Requires AgrusScanner.exe --mcp-only running on the configured port.
 */

interface PluginApi {
  registerTool(tool: ToolDef): void;
  registerService(svc: ServiceDef): void;
  logger: { info(msg: string): void; error(msg: string): void };
  config: { mcpUrl?: string };
}

interface ToolDef {
  name: string;
  description: string;
  parameters: Record<string, unknown>;
  handler(input: Record<string, unknown>): Promise<unknown>;
}

interface ServiceDef {
  id: string;
  start(): void;
  stop(): void;
}

async function callMcp(
  mcpUrl: string,
  tool: string,
  args: Record<string, unknown>
): Promise<unknown> {
  const res = await fetch(mcpUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: Date.now(),
      method: "tools/call",
      params: { name: tool, arguments: args },
    }),
  });

  if (!res.ok) {
    throw new Error(`MCP request failed: ${res.status} ${res.statusText}`);
  }

  const data = (await res.json()) as { result?: { content?: Array<{ text?: string }> }; error?: { message: string } };

  if (data.error) {
    throw new Error(`MCP error: ${data.error.message}`);
  }

  const text = data.result?.content?.[0]?.text;
  return text ? JSON.parse(text) : data.result;
}

export default function register(api: PluginApi) {
  const mcpUrl = api.config.mcpUrl || "http://localhost:8999/mcp";

  api.registerTool({
    name: "scan_network",
    description:
      "Scan an IP range: ping sweep, port scan, DNS resolution, and optional AI/ML service detection. Returns JSON array of host results.",
    parameters: {
      type: "object",
      required: ["ip_range"],
      properties: {
        ip_range: {
          type: "string",
          description:
            'IP range in CIDR (192.168.1.0/24) or range (10.0.0.1-254) format',
        },
        preset: {
          type: "string",
          enum: ["quick", "common", "extended", "ai", "none"],
          default: "quick",
          description: "Port preset to use",
        },
        skip_ping: {
          type: "boolean",
          default: false,
          description: "Scan all IPs regardless of ping response",
        },
      },
    },
    handler: async (input) => {
      return callMcp(mcpUrl, "scan_network", input);
    },
  });

  api.registerTool({
    name: "probe_host",
    description:
      "Deep-scan a single IP address: port scan and AI/ML service detection. Returns JSON object with full host detail.",
    parameters: {
      type: "object",
      required: ["ip"],
      properties: {
        ip: {
          type: "string",
          description: "Single IP address to probe",
        },
        ports: {
          type: "string",
          default: "ai",
          description:
            "Comma-separated ports, or preset name: quick, common, extended, ai",
        },
      },
    },
    handler: async (input) => {
      return callMcp(mcpUrl, "probe_host", input);
    },
  });

  api.registerTool({
    name: "list_presets",
    description:
      "List available scan presets with their port counts and port numbers.",
    parameters: {
      type: "object",
      properties: {},
    },
    handler: async () => {
      return callMcp(mcpUrl, "list_presets", {});
    },
  });

  api.logger.info(
    `Agrus Scanner plugin loaded — MCP endpoint: ${mcpUrl}`
  );
}
