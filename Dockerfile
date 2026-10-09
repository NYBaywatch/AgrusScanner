# Agrus Scanner MCP server (headless, cross-platform).
#   docker build -t agrus-mcp .
#   docker run -i --rm agrus-mcp                    # stdio (MCP clients, Glama)
#   docker run --rm -p 8999:8999 agrus-mcp --http   # Streamable HTTP at /mcp
# Scanning your LAN from a container needs --network host (Linux) or a routed network.

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
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8999
EXPOSE 8999
ENTRYPOINT ["dotnet", "agrus-mcp.dll"]
