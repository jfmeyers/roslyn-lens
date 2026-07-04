// @ts-check
import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

// GitHub Pages project site: https://jfmeyers.github.io/roslyn-lens/
export default defineConfig({
  site: 'https://jfmeyers.github.io',
  base: '/roslyn-lens/',
  trailingSlash: 'ignore',
  integrations: [sitemap()],
  // The docs pages are generated at build time from ../docs via Node fs,
  // so allow Vite to read one level above the website root.
  vite: {
    server: { fs: { allow: ['..'] } },
  },
});
