export const SITE = {
  name: 'RoslynLens',
  tagline: 'Token-efficient .NET codebase navigation for AI agents',
  description:
    'An MCP server that gives Claude, Cursor, and Copilot a semantic, read-only view of your C# solution via Roslyn — so agents navigate by structure instead of re-reading source.',
  github: 'https://github.com/jfmeyers/roslyn-lens',
  nuget: 'https://www.nuget.org/packages/RoslynLens',
};

export const STATS = [
  { value: '77%', label: 'fewer tokens', sub: 'pooled reduction, self-hosted benchmark' },
  { value: '34', label: 'MCP tools', sub: 'navigation, analysis & architecture' },
  { value: '18', label: 'anti-pattern detectors', sub: 'async, security, EF Core & domain' },
  { value: '.NET 10', label: 'Roslyn 5.6', sub: 'analyzes any C# target framework' },
];

export interface Feature {
  icon: string;
  title: string;
  body: string;
}

export const FEATURES: Feature[] = [
  {
    icon: 'search',
    title: 'Semantic navigation',
    body: 'Find symbols, references, callers, implementations and overrides across the whole solution — resolved by Roslyn, not text search.',
  },
  {
    icon: 'graph',
    title: 'Architecture graphs',
    body: 'Namespace communities, god nodes, surprising cross-namespace dependencies, circular dependencies and isolated symbols reveal coupling and layering issues.',
  },
  {
    icon: 'warning',
    title: 'Anti-pattern detection',
    body: '18 built-in detectors for async/await, HttpClient, DateTime, EF Core, logging, secrets and more — plus opt-in Roslynator (500+ rules).',
  },
  {
    icon: 'tokens',
    title: 'Token efficiency',
    body: 'Responses are 30–150 tokens of structured JSON. A reproducible, offline benchmark measures 77% pooled / 73% median reduction versus reading source.',
  },
  {
    icon: 'compass',
    title: 'Zero-config discovery',
    body: 'BFS auto-discovery finds your .sln/.slnx, with multi-solution switching at runtime. No manual path wiring.',
  },
  {
    icon: 'lock',
    title: 'Read-only by design',
    body: 'Every tool advertises MCP read-only/idempotent hints. RoslynLens inspects your code; it never edits it.',
  },
];

export interface ToolGroup {
  label: string;
  tools: string[];
}

export const TOOL_GROUPS: ToolGroup[] = [
  {
    label: 'Navigation',
    tools: ['find_symbol', 'find_references', 'find_callers', 'find_implementations', 'find_overrides', 'find_dead_code'],
  },
  {
    label: 'Inspection',
    tools: ['get_symbol_detail', 'get_public_api', 'get_type_hierarchy', 'get_file_overview', 'get_type_overview', 'resolve_external_source'],
  },
  {
    label: 'Analysis',
    tools: ['analyze_method', 'analyze_control_flow', 'analyze_data_flow', 'get_complexity_metrics', 'detect_antipatterns', 'detect_duplicates'],
  },
  {
    label: 'Architecture graphs',
    tools: ['get_communities', 'find_god_nodes', 'find_surprising_dependencies', 'find_isolated_symbols', 'detect_circular_dependencies', 'get_dependency_graph'],
  },
  {
    label: 'Project & solution',
    tools: ['get_project_graph', 'get_diagnostics', 'get_test_coverage_map', 'get_module_depends_on', 'list_solutions', 'switch_solution'],
  },
  {
    label: 'Batch',
    tools: ['find_symbols_batch', 'get_symbol_detail_batch', 'get_public_api_batch', 'validate_granit_conventions'],
  },
];

export interface InstallOption {
  id: string;
  label: string;
  code: string;
  note: string;
}

export const INSTALL: InstallOption[] = [
  {
    id: 'tool',
    label: 'dotnet tool',
    code: 'dotnet tool install --global RoslynLens',
    note: 'Global .NET tool. Requires the .NET 10 SDK.',
  },
  {
    id: 'claude',
    label: 'Claude Code',
    code: 'claude mcp add --scope user --transport stdio roslyn-lens -- roslyn-lens',
    note: 'Register the stdio server with Claude Code.',
  },
  {
    id: 'mcpb',
    label: 'Claude Desktop',
    code: 'npx @anthropic-ai/mcpb pack mcpb roslyn-lens.mcpb',
    note: 'Pack the bundle, then drag the .mcpb into Settings → Extensions.',
  },
  {
    id: 'docker',
    label: 'Docker',
    code: 'docker run --rm -i -v "$PWD":/workspace roslyn-lens',
    note: 'SDK-based image; mount the repo to analyse at /workspace.',
  },
];
