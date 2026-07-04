import fs from 'node:fs';
import path from 'node:path';
import { Marked } from 'marked';
import { markedHighlight } from 'marked-highlight';
import hljs from 'highlight.js';

const DOCS_ROOT = path.resolve(process.cwd(), '..', 'docs');
const BASE = import.meta.env.BASE_URL.replace(/\/$/, ''); // e.g. "/roslyn-lens"
const GITHUB_BLOB = 'https://github.com/jfmeyers/roslyn-lens/blob/main';

export interface TocItem {
  id: string;
  text: string;
  depth: number;
}

export interface DocEntry {
  /** '' for the docs index (README.md), else e.g. "getting-started/installation" */
  slug: string;
  /** Astro [...slug] param — undefined for the index */
  param: string | undefined;
  title: string;
  group: string;
  groupLabel: string;
  order: number;
  html: string;
  toc: TocItem[];
  githubPath: string;
}

const GROUPS: Record<string, { label: string; order: number }> = {
  overview: { label: 'Overview', order: 0 },
  'getting-started': { label: 'Getting started', order: 1 },
  tools: { label: 'Tools', order: 2 },
  detectors: { label: 'Detectors', order: 3 },
  architecture: { label: 'Architecture', order: 4 },
};

// Curated ordering within each group; anything not listed sorts after, by title.
const ORDER: string[] = [
  'README', 'comparison', 'BENCHMARKS',
  'getting-started/quickstart', 'getting-started/installation',
  'getting-started/configuration', 'getting-started/configuration-reference',
  'getting-started/troubleshooting',
  'tools/navigation', 'tools/inspection', 'tools/analysis', 'tools/compound',
  'tools/graph', 'tools/project', 'tools/solution', 'tools/batch',
  'tools/advanced', 'tools/modular',
  'detectors/general', 'detectors/domain',
  'architecture/how-it-works', 'architecture/adding-a-tool', 'architecture/adding-a-detector',
];

function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/<[^>]+>/g, '')
    .replace(/[^\w\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-');
}

function walk(dir: string, acc: string[] = []): string[] {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, acc);
    else if (entry.name.endsWith('.md')) acc.push(full);
  }
  return acc;
}

function toRoute(targetSlug: string): string {
  return targetSlug === 'README' ? `${BASE}/docs` : `${BASE}/docs/${targetSlug}`;
}

/** Rewrite a markdown href relative to the source file's directory (relative to DOCS_ROOT). */
function rewriteHref(href: string, fileRelDir: string): string {
  if (/^(https?:|mailto:|#|\/)/i.test(href)) return href;

  const hashIndex = href.indexOf('#');
  const anchor = hashIndex >= 0 ? href.slice(hashIndex) : '';
  const pathPart = hashIndex >= 0 ? href.slice(0, hashIndex) : href;
  if (!pathPart) return href;

  if (pathPart.endsWith('.md')) {
    const targetRel = path.posix.normalize(path.posix.join(fileRelDir, pathPart)).replace(/\.md$/, '');
    return toRoute(targetRel) + anchor;
  }

  // Non-markdown relative link — point at the file on GitHub (resolved from repo root).
  const repoRel = path.posix.normalize(path.posix.join('docs', fileRelDir, pathPart));
  return `${GITHUB_BLOB}/${repoRel}${anchor}`;
}

function buildMarked(fileRelDir: string, toc: TocItem[]): Marked {
  const marked = new Marked(
    markedHighlight({
      langPrefix: 'hljs language-',
      highlight(code, lang) {
        const language = lang && hljs.getLanguage(lang) ? lang : 'plaintext';
        return hljs.highlight(code, { language }).value;
      },
    }),
  );

  marked.use({
    renderer: {
      heading({ tokens, depth }) {
        const text = this.parser.parseInline(tokens);
        const id = slugify(text);
        if (depth === 2 || depth === 3) toc.push({ id, text: text.replace(/<[^>]+>/g, ''), depth });
        return `<h${depth} id="${id}">${text}</h${depth}>\n`;
      },
      link({ href, title, tokens }) {
        const text = this.parser.parseInline(tokens);
        const url = rewriteHref(href, fileRelDir);
        const external = /^https?:/i.test(url);
        const attrs = [`href="${url}"`];
        if (title) attrs.push(`title="${title}"`);
        if (external) attrs.push('target="_blank"', 'rel="noopener noreferrer"');
        return `<a ${attrs.join(' ')}>${text}</a>`;
      },
    },
  });

  return marked;
}

function deriveTitle(markdown: string, slug: string): string {
  const match = markdown.match(/^#\s+(.+?)\s*$/m);
  if (match) return match[1].replace(/`/g, '');
  const base = slug.split('/').pop() ?? slug;
  return base.replace(/-/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}

let cache: DocEntry[] | null = null;

export function getDocs(): DocEntry[] {
  if (cache) return cache;

  const files = walk(DOCS_ROOT);
  const entries: DocEntry[] = files.map((file) => {
    const rel = path.relative(DOCS_ROOT, file).split(path.sep).join('/');
    const slug = rel.replace(/\.md$/, '');
    const fileRelDir = path.posix.dirname(rel);
    const markdown = fs.readFileSync(file, 'utf8');

    const isIndex = slug === 'README';
    const groupKey = slug.includes('/') ? slug.split('/')[0] : 'overview';
    const group = GROUPS[groupKey] ? groupKey : 'overview';

    const toc: TocItem[] = [];
    const html = buildMarked(fileRelDir === '.' ? '' : fileRelDir, toc).parse(markdown) as string;

    const orderIndex = ORDER.indexOf(slug);
    return {
      slug: isIndex ? '' : slug,
      param: isIndex ? undefined : slug,
      title: deriveTitle(markdown, slug),
      group,
      groupLabel: GROUPS[group].label,
      order: orderIndex === -1 ? 999 : orderIndex,
      html,
      toc,
      githubPath: `docs/${rel}`,
    };
  });

  entries.sort((a, b) => a.order - b.order || a.title.localeCompare(b.title));
  cache = entries;
  return entries;
}

export interface SidebarGroup {
  key: string;
  label: string;
  items: DocEntry[];
}

export function getSidebar(): SidebarGroup[] {
  const docs = getDocs();
  const groups = Object.entries(GROUPS)
    .sort((a, b) => a[1].order - b[1].order)
    .map(([key, meta]) => ({
      key,
      label: meta.label,
      items: docs.filter((d) => d.group === key),
    }))
    .filter((g) => g.items.length > 0);
  return groups;
}

export function docHref(entry: DocEntry): string {
  return entry.slug === '' ? `${BASE}/docs` : `${BASE}/docs/${entry.slug}`;
}
