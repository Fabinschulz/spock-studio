// Fonte de verdade: shared/styles/_tokens.scss (brand-palette.test.ts falha se divergirem).
export const brandPalette = {
  teal400: '#33c1ba',
  teal300: '#4fd3cc',
  onPrimary: '#06201f',
  background: '#151515',
  surface: '#1b1b1b',
  raised: '#232323',
  text: '#ececec',
  textMuted: '#a0a0a0',
  line: 'rgba(255, 255, 255, 0.08)',
  borderStrong: '#737373',
  error: '#f2766e',
  success: '#7bd88f'
} as const;

// Véus translúcidos sobre qualquer superfície: o realce de hover e de seleção é neutro, sem tinta de cor.
export const veil = {
  hover: 'rgba(255, 255, 255, 0.06)',
  selected: 'rgba(255, 255, 255, 0.1)',
  disabledFill: 'rgba(255, 255, 255, 0.08)',
  disabledText: 'rgba(236, 236, 236, 0.38)',
  activeIcon: 'rgba(236, 236, 236, 0.56)'
} as const;
