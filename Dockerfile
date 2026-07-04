# RoslynLens — MCP server for token-efficient .NET codebase navigation.
#
# The final image ships the .NET SDK, not just the runtime: RoslynLens loads target
# projects through MSBuildWorkspace, which evaluates MSBuild and therefore needs the
# SDK present at runtime. Mount the repository to analyse at /workspace.
#
#   docker build -t roslyn-lens .
#   docker run --rm -i -v "$PWD":/workspace roslyn-lens
#
# The container speaks MCP over stdio, so `-i` (interactive stdin) is required.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the central package manifest first for better layer caching.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/RoslynLens/RoslynLens.csproj src/RoslynLens/
RUN dotnet restore src/RoslynLens/RoslynLens.csproj

COPY . .
RUN dotnet publish src/RoslynLens/RoslynLens.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
LABEL org.opencontainers.image.title="RoslynLens" \
      org.opencontainers.image.description="MCP server for token-efficient .NET codebase navigation via Roslyn" \
      org.opencontainers.image.source="https://github.com/jfmeyers/roslyn-lens" \
      org.opencontainers.image.licenses="Apache-2.0"

COPY --from=build /app /app

# The MCP client mounts the target repository here; discovery starts from the working dir.
WORKDIR /workspace

ENTRYPOINT ["dotnet", "/app/RoslynLens.dll"]
