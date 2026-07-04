# Project Tools

## get_project_graph

Show the solution's project dependency tree. Supports filtering for large
solutions (100+ projects) to avoid output overflow.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Comma-separated project name filter (substring match, e.g. `Granit.AI,Granit.Core`) |
| `includeTransitive` | bool | no | Include transitive dependencies of filtered projects (default: `false`) |
| `maxResults` | int | no | Maximum projects to return (default: `50`) |

**Example prompts:**

- "Show the project dependency graph" (returns first 50 projects)
- "Show project graph for Granit.AI modules" → `projectFilter: "Granit.AI"`
- "Show Granit.Security and all its dependencies" →
  `projectFilter: "Granit.Security", includeTransitive: true`

**Returns:** Each project with its name, target framework, and direct project
references. `Total` reflects the full count of matching projects (even if
truncated by `maxResults`).

---

## get_diagnostics

Get compiler warnings and errors for a file, project, or the entire solution.
Optionally also run the bundled [Roslynator](https://github.com/dotnet/roslynator)
analyzers (500+ rules) for richer style and refactoring suggestions.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `scope` | string | no | `solution` (default), `project`, or `file` |
| `path` | string | no | File or project name/path (required for `file` and `project`) |
| `severityFilter` | string | no | `error`, `warning` (default), `info`, or `hidden` |
| `includeAnalyzers` | bool | no | Also run Roslynator analyzers (default: `false`) |

**Example prompt:** "Show all compiler errors in MyApp.Security"

**Returns:** Diagnostic ID, severity, message, file path, and line number.

When `includeAnalyzers` is `true`, the server loads the Roslynator analyzers
that ship alongside the tool and runs them over the compilation. This is
noticeably slower than compiler-only diagnostics, so it stays off by default;
lower the `severityFilter` to `info` or `hidden` to surface Roslynator's
suggestion-level rules. Redundant "fade-out" companion diagnostics are dropped
to keep responses token-efficient. If the analyzers cannot be loaded, the tool
transparently falls back to compiler-only diagnostics.

---

## get_test_coverage_map

Heuristic mapping of which tests cover which production types, based on naming
conventions and project references.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Filter by project name |
| `maxResults` | int | no | Max results to return (default: 50) |

**Example prompt:** "Show test coverage map for MyApp.Validation"

**Returns:** Production types mapped to their corresponding test classes (if found).
