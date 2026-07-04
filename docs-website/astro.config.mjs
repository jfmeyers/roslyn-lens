// @ts-check
import { defineConfig } from 'astro/config';

// GitHub Pages project site: https://jfmeyers.github.io/roslyn-lens/
export default defineConfig({
  site: 'https://jfmeyers.github.io',
  base: '/roslyn-lens/',
  trailingSlash: 'ignore',
  // The docs pages are generated at build time from ../docs via Node fs,
  // so allow Vite to read one level above the website root.
  vite: {
    server: { fs: { allow: ['..'] } },
  },
});
