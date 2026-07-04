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

# Keep the .NET host quiet and self-contained under a writable home so the
# container can run as a non-root user with no first-run/telemetry writes.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_HOME=/home/app

COPY --from=build /app /app

# Run as the non-root "app" user (uid 1654) that the .NET base image already
# ships, rather than root. Analysis is read-only, so read access to the mounted
# repository is enough.
USER $APP_UID

# The MCP client mounts the target repository here; discovery starts from the working dir.
WORKDIR /workspace

ENTRYPOINT ["dotnet", "/app/RoslynLens.dll"]
