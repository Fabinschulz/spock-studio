import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const { version } = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf-8')) as {
  version: string;
};

export default defineConfig({
  plugins: [react()],
  // A versão mostrada no cabeçalho é a do pacote: uma só fonte, sem número digitado à mão.
  define: { __APP_VERSION__: JSON.stringify(version) },
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
