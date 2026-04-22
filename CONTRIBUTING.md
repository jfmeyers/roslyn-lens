# Contributing to RoslynLens

Thank you for your interest in contributing! This guide will help you get
started.

## Code of Conduct

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before contributing.
We are committed to providing a welcoming and inclusive experience for
everyone.

## Getting Started

### Prerequisites

- **.NET 10 SDK** — `dotnet --version` should return `10.x`
  (the [`global.json`](global.json) pins the minimum SDK)
- **Git** with SSH access
- *Optional but recommended*: an MCP-capable host (e.g. Claude Code) to
  exercise the server end-to-end

### Setup

```bash
git clone git@github.com:jfmeyers/roslyn-lens.git
cd roslyn-lens
dotnet restore
```

### Build and test

```bash
# Build the solution
dotnet build

# Run all tests (xUnit v3 + Shouldly)
dotnet test

# Pack as a global tool and install locally
dotnet pack -c Release -o ./nupkgs
dotnet tool install --global --add-source ./nupkgs RoslynLens
```

## How to Contribute

### Reporting Bugs

Open an issue using the **Bug Report** template. Include:

- A clear, concise description of the problem
- Steps to reproduce (ideally with a minimal `.sln`)
- Expected vs actual behavior
- The MCP tool call that triggered it (name + arguments)
- `dotnet --info` output and OS

### Suggesting Features

Open an issue using the **Feature Request** template. Describe:

- The use case and motivation
- Whether it is a new MCP tool, a new anti-pattern detector, or a
  navigation improvement
- The expected token cost of the response (RoslynLens aims for
  30–150 tokens per query)
- Any alternatives you considered

### Submitting Changes

1. **Fork** the repository
2. **Create a branch** from `main`:

   ```text
   <type>/<short-description>

   Types: feature/ | fix/ | docs/ | refactor/ | chore/ | test/ | perf/
   ```

3. **Write your code** following the conventions below
4. **Write or update tests** in `tests/RoslynLens.Tests/`
5. **Run the Definition of Done checks** (see below)
6. **Commit** using [Conventional Commits](https://www.conventionalcommits.org/):

   ```bash
   git commit -m "feat(tools): add find_overrides_batch"
   git commit -m "fix(workspace): handle reload race"
   git commit -m "docs: clarify multi-solution discovery"
   ```

7. **Open a pull request** against `main`

### Definition of Done

All checks are **blocking** — a PR will not be merged until they pass:

1. `dotnet build` — zero warnings (`TreatWarningsAsErrors` is enabled)
2. `dotnet test` — all tests pass
3. `npx markdownlint-cli2 "<file>"` — every modified `.md` file passes
4. `dotnet list package --vulnerable --include-transitive` — no known
   vulnerabilities introduced
5. Documentation updated if the change affects a public MCP tool, a
   detector ID, or user-facing behavior

## Code Conventions

### Project layout

| Path | Role |
| ---- | ---- |
| `src/RoslynLens/Tools/` | One file per MCP tool |
| `src/RoslynLens/Analyzers/` | One file per detector (`AP*` general, `GR-*` domain) |
| `src/RoslynLens/Responses/` | Token-optimized DTOs (records) |
| `tests/RoslynLens.Tests/` | xUnit v3 + Shouldly, mirrors `src/` layout |

### C# style

- **Target**: `net10.0`, `LangVersion=14`, `Nullable=enable`,
  `ImplicitUsings=enable`
- `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` —
  CI will fail on warnings
- One public type per file
- Prefer `record` types for DTOs in `Responses/`

### Logging

- **stderr only** — `stdout` is reserved for JSON-RPC (MCP protocol).
  Never `Console.WriteLine` from production code.

### Token efficiency

RoslynLens exists to reduce token consumption. Every new tool or detector
must:

- Return the smallest payload that conveys the information
- Avoid embedding full file contents — return locations + summaries instead
- Be benchmarked informally against an equivalent `Read` + `Grep`
  combination in the PR description

### Adding a new detector

See `CLAUDE.md` for the full recipe. Short version:

1. Create `src/RoslynLens/Analyzers/MyDetector.cs`
   - Simple invocation match (`Foo.Bar()`) → extend `InvocationDetectorBase`
   - Simple creation match (`new Foo()`) → extend `ObjectCreationDetectorBase`
   - Otherwise → implement `IAntiPatternDetector` directly
2. Pick an ID: `APxxx` (general .NET) or `GR-xxx` (domain-specific)
3. Add a test class in `tests/RoslynLens.Tests/Analyzers/`
4. Detectors must handle `SemanticModel? model = null` gracefully
   (syntax-only mode)

### Adding a new tool

See `CLAUDE.md` for the full recipe. Short version:

1. Create `src/RoslynLens/Tools/MyTool.cs` with `[McpServerToolType]`
2. Tools are auto-discovered via `WithToolsFromAssembly()`
3. Inject `WorkspaceManager` to access the solution/compilations
4. Call `workspace.EnsureReadyOrStatus(ct)` — return its status if not ready

### Tests

- **Framework**: xUnit v3 + Shouldly
- Use `CSharpSyntaxTree.ParseText(source)` — no full compilation needed
  for most detector tests
- Use `TestContext.Current.CancellationToken` (never `CancellationToken.None`)

### Dependencies

- When adding, removing, or upgrading a NuGet package, update
  [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) at the repository
  root
- Non-permissive licenses (GPL, LGPL, AGPL, SSPL) — flag in the PR
  description before proceeding

### Security

**Never**:

- Commit secrets or credentials (not even in tests or examples)
- Log to `stdout` (it breaks the MCP protocol)
- Add a feature that transmits source code over the network

**Always**:

- Treat the analyzed solution as untrusted input — guard against path
  traversal and resource exhaustion
- Surface errors as structured MCP responses, never as crashes

## Review Process

A maintainer will review your PR against this checklist:

- [ ] No hardcoded secrets
- [ ] Tests pass (`dotnet test`)
- [ ] Build clean (`dotnet build`, zero warnings)
- [ ] Detector/tool follows the conventions in `CLAUDE.md`
- [ ] Logs go to stderr, not stdout
- [ ] `THIRD-PARTY-NOTICES.md` updated (if dependencies changed)
- [ ] Documentation updated if applicable

## License

By contributing, you agree that your contributions will be licensed under
the [Apache License 2.0](LICENSE).
