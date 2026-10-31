import { alpha } from '@mui/material/styles';
import { initializeTheme } from '@synthra.io/ui-kit';
import { brandPalette as c } from './brand-palette';

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

// O Button da Synthra lê a tipografia de `subtitle2` (médio) e `caption` (pequeno) nos seus overrides de tamanho:
// é por aqui, e não por `styleOverrides.root`, que o texto dos botões segue a escala de 13 px do aplicativo.
const typography = {
  fontFamily: FONT_SANS,
  ...Object.fromEntries(VARIANTS.map((variant) => [variant, { fontFamily: FONT_SANS }])),
  subtitle2: { fontFamily: FONT_SANS, fontSize: '0.8125rem', fontWeight: 600, lineHeight: '20px' },
  caption: { fontFamily: FONT_SANS, fontSize: '0.75rem', fontWeight: 500, lineHeight: '16px' }
};

const palette = {
  primary: { light: c.teal300, main: c.teal400, dark: c.teal300, contrastText: c.onPrimary },
  secondary: { light: c.teal300, main: c.teal400, dark: c.teal300, contrastText: c.onPrimary },
  background: { default: c.background, paper: c.surface },
  text: { primary: c.text, secondary: c.textMuted },
  divider: c.line,
  error: { main: c.error },
  success: { main: c.success },
  brand: {
    lightest: alpha(c.teal400, 0.12),
    light: c.teal300,
    medium: c.teal400,
    dark: c.teal400,
    darkest: c.teal400
  },
  action: {
    hover: alpha(c.teal400, 0.08),
    selected: alpha(c.teal400, 0.12),
    focus: alpha(c.teal400, 0.16),
    disabledBackground: 'rgba(160, 200, 196, 0.12)',
    disabled: 'rgba(232, 240, 239, 0.38)',
    active: alpha(c.teal400, 0.56)
  }
};

/**
 * A Synthra continua sendo a biblioteca oficial: a identidade entra por `overrides` do tema (paleta, forma,
 * tipografia), sem trocar nem bifurcar componentes. Tema escuro único (decisão de produto).
 */
export const theme = initializeTheme({
  mode: 'dark',
  overrides: {
    // `brand` é um campo próprio da Synthra, fora da tipagem de PaletteOptions do MUI.
    palette: palette as never,
    typography,
    shape: { borderRadius: 6 },
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: ({ ownerState }) => ({
            textTransform: 'none',
            minHeight: 34,
            // Ação secundária discreta: contorno de controle (≥ 3:1) em vez do contorno branco padrão do "inherit".
            ...(ownerState.color === 'inherit' &&
              ownerState.variant === 'outlined' && {
                borderColor: c.borderStrong,
                color: c.text,
                '&:hover': { borderColor: c.textMuted, backgroundColor: 'rgba(160, 200, 196, 0.08)' }
              }),
            ...(ownerState.color === 'inherit' &&
              ownerState.variant === 'text' && {
                color: c.textMuted,
                '&:hover': { backgroundColor: 'rgba(160, 200, 196, 0.08)', color: c.text }
              })
          })
        }
      },
      MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }
    }
  }
});
