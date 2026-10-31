/**
 * Cores que o tema da Synthra (MUI) precisa em TypeScript. A fonte de verdade é shared/styles/_tokens.scss;
 * brand-palette.test.ts falha se os valores divergirem.
 */
export const brandPalette = {
  teal400: '#33c1ba', // marca
  teal300: '#4fd3cc', // hover e texto na cor da marca
  onPrimary: '#06201f',
  background: '#0a0f10',
  surface: '#0f1617',
  text: '#e8f0ef',
  textMuted: '#a5b5b4',
  line: 'rgba(160, 200, 196, 0.14)',
  borderStrong: '#5f7a7a',
  error: '#f2766e',
  success: '#7bd88f'
} as const;
