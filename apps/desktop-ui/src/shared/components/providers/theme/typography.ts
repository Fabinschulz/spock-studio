const FONT_SANS = "'Inter', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif";

const VARIANTS = [
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'subtitle1',
  'subtitle2',
  'body1',
  'body2',
  'button',
  'caption',
  'overline',
  'xg',
  'xxxl',
  'xxl'
] as const;

// O Button da Synthra lê o tamanho do texto de `subtitle2` e `caption`; `styleOverrides.root` não o altera.
export const typography = {
  fontFamily: FONT_SANS,
  ...Object.fromEntries(VARIANTS.map((variant) => [variant, { fontFamily: FONT_SANS }])),
  subtitle2: { fontFamily: FONT_SANS, fontSize: '0.8125rem', fontWeight: 600, lineHeight: '20px' },
  caption: { fontFamily: FONT_SANS, fontSize: '0.75rem', fontWeight: 500, lineHeight: '16px' }
};
