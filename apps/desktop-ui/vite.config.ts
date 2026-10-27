import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  // O Tauri serve os arquivos do pacote, não um servidor na raiz.
  base: './',
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@contracts': fileURLToPath(new URL('../../packages/contracts', import.meta.url))
    }
  },
  css: { modules: { localsConvention: 'camelCaseOnly' } },
  server: { port: 1420, strictPort: true, fs: { allow: ['../..'] } },
  build: { target: 'es2022' },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: { modules: { classNameStrategy: 'non-scoped' } },
    include: ['src/**/*.test.{ts,tsx}']
  }
});
