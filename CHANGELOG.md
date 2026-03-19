# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-03-19

### Added

- `get_complexity_metrics` tool — cyclomatic, cognitive complexity, nesting depth, LOC
- `analyze_method` compound tool — signature + callers + dependencies + complexity
- `get_type_overview` compound tool — public API + hierarchy + implementations + diagnostics
- `get_file_overview` compound tool — types + diagnostics + anti-patterns for a file
- `find_symbols_batch` tool — resolve multiple symbol names in one call
- `get_public_api_batch` tool — get public API of multiple types in one call
- `get_symbol_detail_batch` tool — get details of multiple symbols in one call
- `analyze_data_flow` tool — variable assignments, reads, writes, captured variables
- `analyze_control_flow` tool — reachability, return points, exit points
- `resolve_external_source` tool — SourceLink + decompilation for NuGet dependencies
- `detect_duplicates` tool — structurally similar code detection via AST fingerprinting
- Glob pattern search (`*Service`, `Get*User`) in `find_symbol`
- Fuzzy FQN resolution with partial namespace matching and Levenshtein distance
- Dead code analysis filters (`includePublicMembers`, `includeEntryPoints`,
  `projectFilter`, `fileFilter`)
- Environment variable configuration (`ROSLYN_NAV_TIMEOUT_SECONDS`,
  `ROSLYN_NAV_MAX_RESULTS`, `ROSLYN_NAV_CACHE_SIZE`, `ROSLYN_NAV_LOG_LEVEL`)
- ICSharpCode.Decompiler dependency for external source resolution

### Changed

- `find_symbol` now supports glob patterns (`*`, `?`) alongside substring matching
- `find_dead_code` now accepts granular filter parameters
- `WorkspaceManager` cache size is now configurable via environment variables
- `SymbolResolver` falls back to fuzzy resolution when exact match fails

## [1.0.0] - 2026-03-14

### Added

- 17 MCP navigation tools (`find_symbol`, `find_references`, `find_implementations`,
  `find_callers`, `find_overrides`, `find_dead_code`, `get_type_hierarchy`,
  `get_public_api`, `get_symbol_detail`, `get_project_graph`, `get_dependency_graph`,
  `get_diagnostics`, `get_test_coverage_map`, `detect_antipatterns`,
  `detect_circular_dependencies`, `get_module_depends_on`, `validate_conventions`)
- 19 anti-pattern detectors (AP001-AP009, GR-GUID, GR-SECRET, GR-SYNC-EF,
  GR-BADREQ, GR-REGEX, GR-SLEEP, GR-CONSOLE, GR-CFGAWAIT, GR-DTO)
- BFS solution auto-discovery (`.sln`/`.slnx`, max 3 levels)
- LRU compilation cache (50 entries)
- File watcher for incremental updates
- Background async solution loading
- Cross-project symbol resolution via `SymbolResolver`
- SonarCloud integration with 80%+ code coverage
- GitHub Actions CI/CD (build + release + NuGet publish)
- Global dotnet tool distribution (`dotnet tool install --global RoslynLens`)
- Comprehensive documentation in `docs/`

### Changed

- Upgraded actions to v5 / Node.js 24
- Extracted shared method resolution to `SymbolResolver`

## [0.1.1] - 2026-03-14

### Added

- Initial release — Roslyn MCP server for Claude Code

[1.1.0]: https://github.com/jfmeyers/roslyn-lens/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/jfmeyers/roslyn-lens/compare/v0.1.1...v1.0.0
[0.1.1]: https://github.com/jfmeyers/roslyn-lens/releases/tag/v0.1.1
