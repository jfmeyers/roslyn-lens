# Third-Party Notices — RoslynLens

This file lists the third-party libraries used by the **RoslynLens**
project along with their respective licenses. It is updated whenever an
external dependency is added or modified.

Last updated: 2026-07-04

---

## License summary

| License | Package count |
| ------- | ------------- |
| MIT | 7 |
| Apache-2.0 | 2 |

---

## Production dependencies

### MIT

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Microsoft.Build.Locator | 1.11.2 | © Microsoft Corporation |
| Microsoft.Build.Framework | 18.7.1 | © Microsoft Corporation |
| Microsoft.NET.StringTools | 18.7.1 | © Microsoft Corporation |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.6.0 | © Microsoft Corporation |
| Microsoft.CodeAnalysis.Workspaces.MSBuild | 5.6.0 | © Microsoft Corporation |
| Microsoft.Extensions.Hosting | 10.0.9 | © Microsoft Corporation |
| ICSharpCode.Decompiler | 10.1.0.8386 | © Daniel Grunwald and contributors |

### Apache-2.0

| Package | Version | Copyright |
| ------- | ------- | --------- |
| ModelContextProtocol | 1.4.0 | © LF Projects, LLC |
| Roslynator.Analyzers | 4.15.0 | © Josef Pihrt |

Roslynator.Analyzers is bundled with the tool but excluded from RoslynLens's own
compilation; its analyzer assemblies are loaded on demand at runtime.

---

## Development dependencies

These are used only by the `RoslynLens.TokenBenchmark` project and the test
suite. They are **not** part of the distributed `RoslynLens` tool.

### MIT

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Microsoft.ML.Tokenizers | 2.0.0 | © Microsoft Corporation |
| Microsoft.ML.Tokenizers.Data.O200kBase | 2.0.0 | © Microsoft Corporation |
| Microsoft.Bcl.Memory | 10.0.9 | © Microsoft Corporation |
