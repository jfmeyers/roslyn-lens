# RoslynLens website

The marketing + documentation site for RoslynLens, built with
[Astro](https://astro.build). The documentation pages are generated at build
time from the repository's [`../docs`](../docs) markdown — edit the docs there,
not here.

## Develop

```bash
cd docs-website
pnpm install
pnpm dev         # http://localhost:4321/roslyn-lens/
```

## Build

```bash
pnpm build       # static output in dist/
pnpm preview     # serve the built site locally
```

## Structure

| Path | Role |
| ---- | ---- |
| `src/pages/index.astro` | Landing page (hero, stats, features, tools, install) |
| `src/pages/docs/[...slug].astro` | Docs routes, one per `../docs/**/*.md` |
| `src/lib/docs.ts` | Reads the markdown, renders it (marked + highlight.js), rewrites links |
| `src/layouts/` | `Base` (shell) and `Doc` (sidebar + content + TOC) |
| `src/consts.ts` | Site metadata and landing-page content |
| `src/styles/global.css` | Design tokens (light/dark) and prose styles |

## Deployment

Pushes to `main` that touch `docs-website/` or `docs/` trigger
[`.github/workflows/deploy-website.yml`](../.github/workflows/deploy-website.yml),
which builds the site and publishes it to GitHub Pages. Enable Pages once in the
repository settings (**Settings → Pages → Source: GitHub Actions**).
