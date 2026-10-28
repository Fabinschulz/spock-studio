import { readFileSync } from 'node:fs';
import { resolve as resolvePath } from 'node:path';

import { describe, expect, it } from 'vitest';

/**
 * Valida os pares de cor que a interface realmente usa, lendo o próprio _tokens.scss (nada é duplicado aqui).
 * Referência: WCAG 2.2 AA. Texto normal 4,5:1; componentes de interface e indicadores 3:1.
 * Transparências são compostas sobre o fundo adjacente antes do cálculo.
 */

type Rgba = { r: number; g: number; b: number; a: number };
type Scope = Map<string, string>;

// O vitest roda na pasta do pacote (apps/desktop-ui).
const source = readFileSync(resolvePath(process.cwd(), 'src/shared/styles/_tokens.scss'), 'utf8');

function declarations(block: string): Scope {
  const scope: Scope = new Map();
  // O Prettier pode quebrar um valor em várias linhas: o espaço em branco é normalizado antes de resolver.
  for (const match of block.matchAll(/--([\w-]+):\s*([^;]+);/g))
    scope.set(match[1]!, match[2]!.replace(/\s+/g, ' ').trim());
  return scope;
}

const lightStart = source.indexOf('@mixin theme-light');
const darkStart = source.indexOf('@mixin theme-dark');
const rootStart = source.indexOf(':root {');
const light = declarations(source.slice(lightStart, darkStart));
const dark = declarations(source.slice(darkStart, rootStart));
const primitives = declarations(source.slice(rootStart, source.indexOf('@media', rootStart)));

function resolve(name: string, scope: Scope, depth = 0): string {
  const value = scope.get(name) ?? primitives.get(name);
  if (value === undefined) throw new Error(`token --${name} não encontrado`);
  const ref = /^var\(\s*--([\w-]+)\s*\)$/.exec(value);
  if (ref) {
    if (depth > 8) throw new Error(`referência circular em --${name}`);
    return resolve(ref[1]!, scope, depth + 1);
  }
  return value;
}

function parse(color: string): Rgba {
  const hex = /^#([0-9a-f]{6})$/i.exec(color);
  if (hex) {
    const n = parseInt(hex[1]!, 16);
    return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255, a: 1 };
  }
  const rgb = /^rgb\(\s*(\d+)\s+(\d+)\s+(\d+)(?:\s*\/\s*([\d.]+))?\s*\)$/.exec(color);
  if (rgb) return { r: +rgb[1]!, g: +rgb[2]!, b: +rgb[3]!, a: rgb[4] === undefined ? 1 : +rgb[4] };
  throw new Error(`cor não reconhecida: ${color}`);
}

const over = (top: Rgba, bottom: Rgba): Rgba => ({
  r: top.r * top.a + bottom.r * (1 - top.a),
  g: top.g * top.a + bottom.g * (1 - top.a),
  b: top.b * top.a + bottom.b * (1 - top.a),
  a: 1
});

function luminance({ r, g, b }: Rgba): number {
  const channel = (v: number) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function contrast(fg: Rgba, bg: Rgba): number {
  const a = luminance(over(fg, bg));
  const b = luminance(bg);
  const [hi, lo] = a > b ? [a, b] : [b, a];
  return (hi + 0.05) / (lo + 0.05);
}

const TONES = ['neutral', 'success', 'warning', 'danger', 'info'] as const;
const TILES = ['teal', 'indigo', 'blue', 'orange'] as const;

for (const [themeName, scope] of [
  ['claro', light],
  ['escuro', dark]
] as const) {
  const color = (token: string) => parse(resolve(token, scope));
  const ratio = (fg: string, bg: string) => contrast(color(fg), color(bg));

  describe(`contraste dos tokens (tema ${themeName})`, () => {
    const surfaces = ['color-background', 'color-surface', 'color-surface-raised'] as const;

    it.each(surfaces)('texto principal em %s ≥ 7:1', (surface) => {
      expect(ratio('color-text', surface)).toBeGreaterThanOrEqual(7);
    });

    it.each(surfaces)('texto secundário em %s ≥ 4,5:1', (surface) => {
      expect(ratio('color-text-muted', surface)).toBeGreaterThanOrEqual(4.5);
    });

    it.each(surfaces)('texto e link na cor da marca em %s ≥ 4,5:1', (surface) => {
      expect(ratio('color-primary-text', surface)).toBeGreaterThanOrEqual(4.5);
    });

    it.each(['color-primary', 'color-primary-hover', 'color-primary-active'])(
      'texto sobre o botão primário (%s) ≥ 4,5:1',
      (state) => {
        expect(ratio('color-on-primary', state)).toBeGreaterThanOrEqual(4.5);
      }
    );

    it('texto da marca sobre o realce translúcido (card selecionado) ≥ 4,5:1', () => {
      const subtle = over(color('color-primary-subtle'), color('color-surface'));
      expect(contrast(color('color-primary-text'), subtle)).toBeGreaterThanOrEqual(4.5);
      expect(contrast(color('color-text'), subtle)).toBeGreaterThanOrEqual(7);
    });

    it.each(TONES)('selo %s: texto sobre o próprio fundo ≥ 4,5:1', (tone) => {
      expect(ratio(`tone-${tone}-fg`, `tone-${tone}-bg`)).toBeGreaterThanOrEqual(4.5);
    });

    it.each(['color-error', 'color-success'])('%s como texto sobre o fundo ≥ 4,5:1', (token) => {
      expect(ratio(token, 'color-background')).toBeGreaterThanOrEqual(4.5);
    });

    it.each(['color-background', 'color-surface', 'color-surface-raised'])(
      'borda de controle contra %s ≥ 3:1 (WCAG 1.4.11)',
      (surface) => {
        expect(ratio('color-border-strong', surface)).toBeGreaterThanOrEqual(3);
      }
    );

    it.each(['color-background', 'color-surface', 'color-surface-raised'])(
      'anel de foco contra %s ≥ 3:1 (WCAG 2.4.11)',
      (surface) => {
        expect(ratio('color-focus', surface)).toBeGreaterThanOrEqual(3);
      }
    );

    it.each(TILES)('ícone da atividade %s sobre o próprio fundo ≥ 3:1 (componente gráfico)', (tile) => {
      expect(ratio(`tile-${tile}-fg`, `tile-${tile}-bg`)).toBeGreaterThanOrEqual(3);
    });

    // O quadrado do ícone é decorativo: o significado está no glifo, e é o par glifo/fundo que a WCAG 1.4.11 exige.

    it('indicador da etapa atual (cor primária) contra o fundo ≥ 3:1', () => {
      expect(ratio('color-primary', 'color-background')).toBeGreaterThanOrEqual(3);
    });
  });
}
