# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-07-04

### Added

- MCP tool annotations on every tool (`readOnlyHint`, `idempotentHint`,
  `openWorldHint`) so hosts can reason about permissions and caching. All tools
  are read-only except `switch_solution`, which is marked writable but
  non-destructive and idempotent
- Per-call invocation logging: each `tools/call` is wrapped in a filter that
  logs a short correlation id, the tool name, wall-clock duration, and
  success/failure/cancellation to stderr — traces a client-visible error back
  to a specific server log line
- `benchmarks/RoslynLens.TokenBenchmark` — a reproducible, offline token-savings
  benchmark that compares full source files against the real `get_public_api`
  output using the GPT-4o (`o200k_base`) tokenizer; writes `docs/BENCHMARKS.md`
  (77% pooled / 73% median reduction self-hosted)
- `Dockerfile` + `.dockerignore` for containerized distribution (SDK-based image,
  required because `MSBuildWorkspace` evaluates MSBuild at runtime)
- MCP Bundle manifest under `mcpb/` for one-click installation in Claude Desktop
  (packs to a `.mcpb` via `npx @anthropic-ai/mcpb pack`); launches the server
  with `dnx` and exposes solution path, timeout, cache size, log level, and max
  results through the bundle's settings UI
- `ROSLYN_LENS_SOLUTION` environment variable to point at a `.sln`/`.slnx` (or a
  directory to search) without a CLI argument; resolution order is now
  `--solution` arg > `ROSLYN_LENS_SOLUTION` > working-directory auto-discovery
- Documentation website under `docs-website/` — an Astro site generated from the
  existing `docs/**` markdown (landing page, sidebar docs, light/dark theme),
  deployed to GitHub Pages via `.github/workflows/deploy-website.yml`
- `get_diagnostics` gains an opt-in `includeAnalyzers` parameter (default
  `false`) that runs the bundled Roslynator analyzers (500+ rules) over the
  compilation. The analyzers ship next to the tool and load reflectively at
  runtime; redundant "fade-out" companion diagnostics are filtered out, and the
  tool falls back to compiler-only diagnostics if they cannot be loaded. The
  `file` scope and `info`/`hidden` severity filters are now documented

### Changed

- Roslynator.Analyzers 4.15.0 bundled with the tool (Apache-2.0), excluded from
  RoslynLens's own compilation to avoid affecting its build
- Microsoft.ML.Tokenizers 2.0.0 + Data.O200kBase 2.0.0 added (benchmark-only,
  not shipped in the tool); Microsoft.Bcl.Memory pinned to 10.0.9 to override a
  vulnerable 9.0.4 pulled transitively (GHSA-73j8-2gch-69rq)

## [1.3.0] - 2026-06-08

### Added

- `get_communities` — partitions solution namespaces into cohesive communities
  using label-propagation over the type-reference graph; returns per-community
  cohesion score (0–1) and common namespace prefix
- `find_god_nodes` — identifies types or methods whose incoming reference count
  exceeds mean + N×stddev across the solution; exposes coupling hotspots most
  likely to benefit from decomposition
- `find_surprising_dependencies` — scores cross-namespace edges on multiple
  surprise factors (cross-assembly, peripheral-source, hub-target, semantic
  distance) and returns the least-expected couplings in the codebase
- `find_isolated_symbols` — finds types with degree 0 in the full solution
  graph (no incoming references and no structural outgoing references to other
  solution types); distinct from `find_dead_code` which only checks incoming
  references

### Changed

- ModelContextProtocol 1.2.0 → 1.4.0
- Microsoft.Extensions.Hosting 10.0.6 → 10.0.8
- Microsoft.Build.Framework 18.4.0 → 18.6.3
- Microsoft.NET.StringTools 18.6.3 added (new transitive dep of `Microsoft.Build.Framework`,
  pinned with `ExcludeAssets="runtime"` to satisfy `Microsoft.Build.Locator`)
- ICSharpCode.Decompiler 10.0.0.8330 → 10.1.0.8386
- coverlet.collector 8.0.1 → 10.0.1 (test)
- Microsoft.NET.Test.Sdk 18.3.0 → 18.6.0 (test)

## [1.2.2] - 2026-04-14

### Added

- Multi-solution hint surfaced in `EnsureReadyOrStatus` status
  response (visible during workspace loading)
- Multi-solution hint surfaced in solution-scoped tool responses:
  `get_project_graph`, `get_diagnostics` (solution scope),
  `find_dead_code` (solution scope), `get_test_coverage_map`,
  `validate_conventions`
- `WorkspaceManager.SerializeWithMultiSolutionHint<T>()` helper
  that wraps payloads as `{ result, hint }` only when multiple
  solutions are discovered (no envelope on single-solution repos)

Addresses [#102](https://github.com/jfmeyers/roslyn-lens/issues/102)
raised by [@ericnewton76](https://github.com/ericnewton76).

## [1.2.1] - 2026-04-14

### Changed

- ModelContextProtocol 1.1.0 → 1.2.0
- Microsoft.Extensions.Hosting 10.0.5 → 10.0.6
- Microsoft.Build.Framework 17.11.48 → 18.4.0
- ICSharpCode.Decompiler 9.1.0.7988 → 10.0.0.8330

## [1.2.0] - 2026-04-14

### Added

- Multi-solution support: discover all `.sln`/`.slnx` files at
  startup instead of silently picking one
- `list_solutions` MCP tool — lists all discovered solutions with
  path, name, and `IsActive` flag
- `switch_solution` MCP tool — switch the active workspace to a
  different discovered solution at runtime
- `SolutionDiscovery.BfsDiscoverAll()` — returns all solutions
  ordered by depth then alphabetically
- `WorkspaceManager.ReloadSolutionAsync()` — full dispose + fresh
  workspace reload with rollback on failure
- `WorkspaceManager.GetMultiSolutionHint()` — contextual hint
  when multiple solutions are present
- Startup warning when multiple solutions are discovered

Thanks to [@ericnewton76](https://github.com/ericnewton76) (Eric
Newton) for the initial implementation (#96).

## [1.1.1] - 2026-03-26

### Fixed

- JSON responses no longer escape quotes as `\u0027` — use relaxed
  encoding for human-readable MCP output
- Environment variable prefix renamed from `ROSLYN_NAV_` to `ROSLYN_LENS_`

### Changed

- NuGet package size reduced from 13MB to 9.2MB by excluding Roslyn
  localization satellites and legacy BuildHost-net472

## [1.1.0] - 2026-03-20

### Changed

- `get_project_graph` now supports filtering by project name (`projectFilter`),
  transitive dependency expansion (`includeTransitive`), and result limiting
  (`maxResults`, default 50) to prevent output overflow on large solutions

## [1.0.0] - 2026-03-19

### Added

- 28 MCP navigation and analysis tools
- 18 anti-pattern detectors (AP001-AP009, GR-GUID, GR-SECRET, GR-SYNC-EF,
  GR-BADREQ, GR-REGEX, GR-SLEEP, GR-CONSOLE, GR-CFGAWAIT, GR-DTO)
- Compound tools (`analyze_method`, `get_type_overview`, `get_file_overview`)
- Batch tools (`find_symbols_batch`, `get_public_api_batch`, `get_symbol_detail_batch`)
- Advanced analysis (`analyze_data_flow`, `analyze_control_flow`,
  `get_complexity_metrics`, `detect_duplicates`, `resolve_external_source`)
- Glob pattern search (`*Service`, `Get*User`) in `find_symbol`
- Fuzzy FQN resolution with partial namespace matching and Levenshtein distance
- Dead code analysis with granular filters (`includePublicMembers`,
  `includeEntryPoints`, `projectFilter`, `fileFilter`)
- Environment variable configuration (`ROSLYN_LENS_TIMEOUT_SECONDS`,
  `ROSLYN_LENS_MAX_RESULTS`, `ROSLYN_LENS_CACHE_SIZE`, `ROSLYN_LENS_LOG_LEVEL`)
- BFS solution auto-discovery (`.sln`/`.slnx`, max 3 levels)
- LRU compilation cache (configurable, default 50 entries)
- File watcher for incremental updates
- Background async solution loading
- Cross-project symbol resolution via `SymbolResolver`
- ICSharpCode.Decompiler for external source resolution
- SonarCloud integration
- GitHub Actions CI/CD (build + release + NuGet publish)
- Global dotnet tool distribution (`dotnet tool install --global RoslynLens`)
- Comprehensive documentation in `docs/`

### Changed

- Renamed project from `JFM.RoslynNavigator` to `RoslynLens`
- NuGet package ID changed to `RoslynLens`
- MCP server name changed to `roslyn-lens`
- Tool command changed to `roslyn-lens`

## [0.1.1] - 2026-03-14

Initial release as `JFM.RoslynNavigator`.

### Added

- 17 MCP navigation tools (find_symbol, find_references, find_callers,
  find_implementations, find_overrides, find_dead_code, get_public_api,
  get_symbol_detail, get_project_graph, get_dependency_graph,
  get_module_depends_on, get_type_hierarchy, get_diagnostics,
  get_test_coverage_map, detect_antipatterns, detect_circular_dependencies,
  validate_granit_conventions)
- 18 anti-pattern detectors
- MSBuildWorkspace with LRU compilation cache
- BFS solution auto-discovery (`.sln`/`.slnx`, max 3 levels)
- Background async solution loading
- GitHub Actions CI/CD (build + release + NuGet publish)
- Global dotnet tool distribution (`dotnet tool install --global JFM.RoslynNavigator`)

[1.3.0]: https://github.com/jfmeyers/roslyn-lens/compare/v1.2.2...v1.3.0
[1.2.2]: https://github.com/jfmeyers/roslyn-lens/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/jfmeyers/roslyn-lens/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/jfmeyers/roslyn-lens/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/jfmeyers/roslyn-lens/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/jfmeyers/roslyn-lens/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/jfmeyers/roslyn-lens/compare/v0.1.1...v1.0.0
[0.1.1]: https://github.com/jfmeyers/roslyn-lens/releases/tag/v0.1.1
