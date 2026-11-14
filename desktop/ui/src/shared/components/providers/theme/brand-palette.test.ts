import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

import { brandPalette, overlay } from './brand-palette';
import { typography } from './typography';

const tokens = readFileSync(resolve(process.cwd(), 'src/shared/styles/_tokens.scss'), 'utf8');

const declared = (name: string): string => {
  const match = new RegExp(`--${name}:\\s*([^;]+);`).exec(tokens);
  if (!match) throw new Error(`token --${name} não encontrado`);
  return match[1]!.trim().toLowerCase();
};

/** A paleta em TypeScript (tema da Synthra) e os tokens SCSS são a mesma identidade: uma só fonte de verdade. */
describe('brandPalette × _tokens.scss', () => {
  it.each([
    ['teal400', 'cyan-400'],
    ['teal300', 'cyan-300'],
    ['background', 'charcoal-950'],
    ['surface', 'charcoal-900'],
    ['raised', 'charcoal-800'],
    ['text', 'ash-50'],
    ['textMuted', 'ash-300'],
    ['borderStrong', 'charcoal-400'],
    ['success', 'mint-400']
  ] as const)('%s coincide com --%s', (key, token) => {
    expect(brandPalette[key]).toBe(declared(token));
  });

  it('cor de texto sobre o primário e erro coincidem com os tokens semânticos', () => {
    expect(brandPalette.onPrimary).toBe(declared('color-on-primary'));
    expect(brandPalette.error).toBe(declared('color-error'));
  });

  it('o vidro das camadas flutuantes coincide com --color-overlay e --backdrop-overlay', () => {
    expect(overlay.background).toBe(declared('color-overlay'));
    expect(overlay.backdrop).toBe(declared('backdrop-overlay'));
  });

  it('a fonte do tema coincide com --font-sans', () => {
    expect(typography.fontFamily.toLowerCase()).toBe(declared('font-sans'));
  });
});
