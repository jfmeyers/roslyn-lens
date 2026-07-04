# Installation

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

## Install from NuGet

```bash
dotnet tool install --global RoslynLens
```

## Install as a Claude Desktop bundle (.mcpb)

RoslynLens ships an [MCP Bundle](https://github.com/anthropics/mcpb) manifest
under [`mcpb/`](../../mcpb/manifest.json) for one-click installation in Claude
Desktop — no manual editing of `claude_desktop_config.json`.

1. Build the bundle (requires Node for the `mcpb` CLI):

   ```bash
   npx @anthropic-ai/mcpb pack mcpb roslyn-lens.mcpb
   ```

2. Open **Claude Desktop → Settings → Extensions** and drag `roslyn-lens.mcpb`
   in (or double-click it).
3. In the extension's settings, set **Solution path** to your `.sln`/`.slnx`
   (or leave it blank to auto-discover), and optionally tune the timeout, cache
   size, log level, and max results.

The bundle does not embed the server: it launches it on demand with `dnx`, the
.NET SDK's NuGet tool runner, so the **.NET 10 SDK is still required** and the
`RoslynLens` package is fetched and cached on first use. The settings you pick
are injected as `ROSLYN_LENS_*` environment variables.

## Install from source

```bash
git clone https://github.com/jfmeyers/roslyn-lens.git
cd roslyn-lens
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs RoslynLens
```

## Verify installation

```bash
roslyn-lens --help
```

## Update

```bash
dotnet tool update --global RoslynLens
```

## Uninstall

```bash
dotnet tool uninstall --global RoslynLens
```
