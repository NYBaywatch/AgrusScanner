# Agrus Scanner MCP server (headless, cross-platform).
#   docker build -t agrus-mcp .
#   docker run -i --rm agrus-mcp                                      # stdio (MCP clients, Glama)
#   docker run --rm -p 8999:8999 -e MCP_TOKEN=secret agrus-mcp --http # Streamable HTTP at /mcp
# Runs as the non-root "app" user. Scanning your LAN needs --network host (Linux).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY AgrusScanner.McpServer/AgrusScanner.McpServer.csproj AgrusScanner.McpServer/
RUN dotnet restore AgrusScanner.McpServer/AgrusScanner.McpServer.csproj
COPY AgrusScanner/Services AgrusScanner/Services
COPY AgrusScanner/Models AgrusScanner/Models
COPY AgrusScanner/Mcp AgrusScanner/Mcp
COPY signatures/catalog.json signatures/catalog.json
COPY AgrusScanner.McpServer AgrusScanner.McpServer
RUN dotnet publish AgrusScanner.McpServer/AgrusScanner.McpServer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# iputils-ping lets .NET fall back to the ping binary for ICMP when running as non-root.
RUN apt-get update && apt-get install -y --no-install-recommends iputils-ping     && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8999
EXPOSE 8999
USER $APP_UID
ENTRYPOINT ["dotnet", "agrus-mcp.dll"]
