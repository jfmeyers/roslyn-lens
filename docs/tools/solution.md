# Solution Management Tools

Tools for working with repositories that contain multiple
`.sln`/`.slnx` files. At startup, RoslynLens discovers all
solutions via BFS and auto-selects the shallowest one. These
tools let you inspect and switch solutions at runtime.

## list_solutions

Lists all discovered solution files with their paths and which
one is currently loaded.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| *(none)* | | | |

**Example prompt:** "What solutions are available?"

**Returns:**

- `Solutions` — array of `{ Path, Name, IsActive }`
- `Hint` — contextual message when multiple solutions exist
  (null when only one solution is found)

Does not require the workspace to be ready (reads static
discovery state).

---

## switch_solution

Switch the active workspace to a different discovered solution.
The path must be one returned by `list_solutions`.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `solutionPath` | string | yes | Full path to the target solution |

**Example prompt:** "Switch to Frontend.slnx"

**Behavior:**

1. Validates the path is in the discovered list
2. Disposes the current workspace (watchers, cache, MSBuild)
3. Loads the new solution
4. On failure, attempts to rollback to the previous solution

**Returns on success:**

```json
{ "status": "switched", "solution": "Frontend.slnx", "projectCount": 12 }
```

**Returns on error:**

```json
{ "error": "Failed to switch solution: ...", "state": "Error" }
```

---

## Hint surfacing in other tools

When multiple solutions are discovered, a contextual hint is also
surfaced in two additional places so agents organically learn about
the multi-solution capability without polluting every tool response:

### 1. Workspace status response

While the workspace is loading or in error state, the JSON returned
by any tool includes a `hint` field:

```json
{
  "state": "Loading",
  "message": "Workspace not ready",
  "projectCount": 0,
  "hint": "hint: 2 solutions discovered. Use list_solutions..."
}
```

### 2. Solution-scoped tools

The following tools wrap their response as `{ result, hint }` when
multiple solutions are discovered:

- `get_project_graph`
- `get_diagnostics` (solution scope only)
- `find_dead_code` (solution scope only)
- `get_test_coverage_map`
- `validate_conventions`

```json
{
  "result": { "Projects": [...], "Total": 5 },
  "hint": "hint: 2 solutions discovered. Use list_solutions..."
}
```

On single-solution repos, responses serialize directly (no envelope,
no hint) to keep token usage minimal.

Token-efficient tools (`find_symbol`, `find_references`, etc.) are
**not** wrapped — they're called in tight loops and the repeated
hint would dominate token cost.
