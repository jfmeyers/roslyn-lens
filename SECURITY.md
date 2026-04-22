# Security Policy

## Supported Versions

| Version | Supported |
| ------- | --------- |
| latest  | Yes       |

Only the latest published version on
[NuGet](https://www.nuget.org/packages/RoslynLens/) receives security
updates. Upgrade with `dotnet tool update --global RoslynLens`.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, send an email to **<jf.meyers@digitaldynamics.be>** with:

- A description of the vulnerability
- Steps to reproduce (proof of concept if possible)
- The affected version(s) of `RoslynLens`
- Any potential impact assessment

### What to expect

- **Acknowledgment** within 48 hours
- **Status update** within 7 days with an assessment and expected timeline
- **Fix or mitigation** as soon as reasonably possible, depending on severity

We follow responsible disclosure practices. We ask that you:

- Allow reasonable time to investigate and address the issue before public
  disclosure
- Avoid exploiting the vulnerability beyond what is necessary to demonstrate it
- Do not access or modify other users' data

## Threat Model

RoslynLens is a **local developer tool**. It runs on the developer's machine,
communicates with the host (typically Claude Code) over `stdio`, and only
reads the `.sln`/`.slnx`/`.cs` files of solutions the developer explicitly
opens. It does not expose a network listener, does not collect telemetry, and
does not transmit source code anywhere.

Areas that are in scope for security reports:

- **Untrusted source code as input** — the tool loads arbitrary C# projects
  via `MSBuildWorkspace`. Any path traversal, command injection, or
  arbitrary code execution triggered solely by opening a malicious
  solution is in scope.
- **Untrusted MCP requests** — the JSON-RPC layer accepts inputs from the
  host. Crafted requests must not allow file access outside the loaded
  workspace, denial-of-service of the host process, or memory exhaustion
  beyond the documented `ROSLYN_LENS_*` limits.
- **Dependencies** — vulnerabilities in transitive packages
  (Roslyn, MSBuild, MCP SDK, ICSharpCode.Decompiler).

Out of scope:

- Issues that require an attacker to already have local code-execution on
  the developer's machine.
- MSBuild build-time code execution from analyzers/targets shipped by the
  analyzed solution itself (this is a property of MSBuild, not RoslynLens).

## Security Design

- **Local-only** — `stdio` transport, no network listener
- **Read-only** — RoslynLens never writes to the analyzed solution
- **No secrets stored** — no credentials, no telemetry, no network calls
  beyond NuGet/SourceLink resolution requested via `resolve_external_source`
- **Dependency scanning** — automated vulnerability scanning in CI/CD
  (`dotnet list package --vulnerable` is part of the release checklist)

## Acknowledgments

We appreciate the security research community and will acknowledge reporters
(with their permission) once the vulnerability is resolved.
