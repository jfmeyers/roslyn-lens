# Graph Intelligence Tools

Four tools that operate on the full solution graph to surface structural
patterns invisible at the file or symbol level: community clustering,
coupling hotspots, unexpected edges, and disconnected components.

---

## get_communities

Partition solution namespaces into cohesive communities using
**label-propagation** over the structural type-reference graph
(inheritance, field/property types, method signatures).
Each community is assigned a cohesion score and a common namespace prefix.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Filter by project name (substring match) |
| `maxIterations` | int | no | Label-propagation iteration cap (default: 20) |
| `maxResults` | int | no | Max communities to return (default: 30) |

**Example prompts:**

- "Show me the natural namespace clusters in the solution"
- "Which namespaces belong to the same subsystem?"

**Returns:** Communities ordered by size, each with member namespaces,
per-namespace degree, cohesion score (0–1), and common prefix.

**Interpreting cohesion:**

| Score | Meaning |
| ----- | ------- |
| 0.8–1.0 | Tightly coupled — good internal cohesion |
| 0.5–0.8 | Moderate — some cross-community leakage |
| < 0.5 | Loosely coupled — candidates for refactoring |

---

## find_god_nodes

Identify types or methods whose **incoming reference count** is
disproportionately high relative to the rest of the solution
(exceeds mean + N × stddev). These are coupling hotspots — worth
reviewing for decomposition or façade extraction.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Filter by project name (substring match) |
| `kind` | string | no | `types` (default) or `methods` |
| `threshold` | float | no | Stddev multiplier for cutoff (default: 2.0) |
| `minRefs` | int | no | Minimum incoming refs to be a candidate (default: 5) |
| `maxResults` | int | no | Max results to return (default: 20) |

**Example prompts:**

- "Which types are referenced the most across the solution?"
- "Find method god nodes in MyApp.Domain" → `kind=methods`
- "Use a stricter threshold" → `threshold=3.0`

**Returns:** Symbols ranked by incoming reference count, with the
computed mean, standard deviation, and threshold used.

---

## find_surprising_dependencies

Detect **semantically unexpected** cross-namespace edges — dependencies
that are structurally present but architecturally surprising. Each edge
is scored on up to four surprise factors:

| Factor | Score | Condition |
| ------ | ----- | --------- |
| `cross-assembly` | +2 | Source and target are in different projects |
| `peripheral-source` | +2 | Source namespace has very few outgoing deps (outlier) |
| `hub-target` | +1 | Target namespace is heavily depended upon (above mean+stddev) |
| `distant-namespaces` | +1 | Namespace prefix distance ≥ 3 segments |

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Filter by project name (substring match) |
| `maxResults` | int | no | Max dependencies to return (default: 20) |

**Example prompts:**

- "Find unexpected cross-namespace couplings in my solution"
- "Show surprising dependencies in MyApp.Infrastructure"

**Returns:** Dependencies ranked by surprise score, each with the list
of triggered factors and the raw reference count.

---

## find_isolated_symbols

Find types with **degree 0** in the full solution graph: no incoming
references from solution code *and* no structural outgoing references
to other solution-defined types (inheritance, field/property types,
method signatures). These are fully disconnected components —
distinct from `find_dead_code`, which only checks incoming references.

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| `projectFilter` | string | no | Filter by project name (substring match) |
| `includePublicTypes` | bool | no | Include public types (default: `false`) |
| `maxResults` | int | no | Max results to return (default: 50) |

**Example prompts:**

- "Find fully isolated types in MyApp.Shared"
- "Find isolated internal types across the solution"

**Difference from `find_dead_code`:**

| Tool | In-degree | Out-degree | Typical cause |
| ---- | --------- | ---------- | ------------- |
| `find_dead_code` | 0 | any | Unused code that still depends on other types |
| `find_isolated_symbols` | 0 | 0 | Disconnected stubs, forgotten scaffolding |

**Returns:** Isolated types with kind, file path, line number, and project.
