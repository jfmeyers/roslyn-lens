import fs from 'node:fs';
import path from 'node:path';
import { Marked } from 'marked';
import { markedHighlight } from 'marked-highlight';
import hljs from 'highlight.js';

const CHANGELOG = path.resolve(process.cwd(), '..', 'CHANGELOG.md');
const REPO = 'https://github.com/jfmeyers/roslyn-lens';

export interface Release {
  version: string;
  date: string; // formatted, e.g. "Jul 4, 2026"
  tag: string;
  url: string;
  bodyHtml: string;
}

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
    link({ href, title, tokens }) {
      const text = this.parser.parseInline(tokens);
      const external = /^https?:/i.test(href);
      const attrs = [`href="${href}"`];
      if (title) attrs.push(`title="${title}"`);
      if (external) attrs.push('target="_blank"', 'rel="noopener noreferrer"');
      return `<a ${attrs.join(' ')}>${text}</a>`;
    },
  },
});

// Reference-style link definitions (e.g. "[1.3.0]: https://…/compare/…") — not content.
const REF_DEF = /^\[[^\]]+\]:\s+\S+/;

function formatDate(iso: string): string {
  const parsed = new Date(`${iso}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) return iso;
  return parsed.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}

/** Parses CHANGELOG.md (Keep a Changelog format) into release entries, newest first. */
export function getReleases(): Release[] {
  const text = fs.readFileSync(CHANGELOG, 'utf8');
  const headerRe = /^##\s+\[([^\]]+)\]\s*(?:-\s*(.+?))?\s*$/;
  const releases: Release[] = [];
  let current: { version: string; date: string; body: string[] } | null = null;

  const flush = () => {
    if (!current) return;
    const body = current.body.filter((line) => !REF_DEF.test(line)).join('\n').trim();
    const tag = `v${current.version}`;
    releases.push({
      version: current.version,
      date: formatDate(current.date),
      tag,
      url: `${REPO}/releases/tag/${tag}`,
      bodyHtml: marked.parse(body) as string,
    });
  };

  for (const line of text.split('\n')) {
    const match = line.match(headerRe);
    if (match) {
      flush();
      current = { version: match[1], date: match[2] ?? '', body: [] };
    } else if (current) {
      current.body.push(line);
    }
  }
  flush();

  return releases; // CHANGELOG is authored newest-first
}
