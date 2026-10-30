import { alpha, type ThemeOptions } from '@mui/material/styles';
import { initializeTheme } from '@synthra.io/ui-kit';
import { brandPalette as c } from './brand-palette';

export type ColorMode = 'light' | 'dark';

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

const typography = {
  fontFamily: FONT_SANS,
  ...Object.fromEntries(VARIANTS.map((variant) => [variant, { fontFamily: FONT_SANS }]))
};

function paletteFor(mode: ColorMode): ThemeOptions['palette'] {
  const dark = mode === 'dark';
  const accent = dark ? c.cyan400 : c.teal700;
  return {
    primary: dark
      ? { light: c.cyan300, main: c.cyan400, dark: c.cyan300, contrastText: c.navy975 }
      : { light: c.teal400, main: c.teal700, dark: c.teal800, contrastText: c.white },
    secondary: dark
      ? { light: c.cyan300, main: c.cyan400, dark: c.cyan300, contrastText: c.navy975 }
      : { light: c.teal400, main: c.teal700, dark: c.teal800, contrastText: c.white },
    background: dark ? { default: c.navy950, paper: c.navy850 } : { default: c.white, paper: c.white },
    text: dark ? { primary: c.ice50, secondary: c.ice300 } : { primary: c.neutral950, secondary: c.neutral600 },
    divider: dark ? c.navy700 : c.neutral200,
    error: { main: dark ? c.errorDark : c.errorLight },
    success: { main: dark ? c.successDark : c.successLight },
    brand: {
      lightest: alpha(dark ? c.cyan400 : c.teal400, 0.12),
      light: dark ? c.cyan300 : c.teal300,
      medium: dark ? c.cyan400 : c.teal400,
      dark: c.teal700,
      darkest: c.teal800
    },
    action: {
      hover: alpha(accent, 0.06),
      selected: alpha(accent, 0.12),
      focus: alpha(accent, 0.16),
      disabledBackground: alpha(accent, 0.12),
      disabled: alpha(dark ? c.white : c.neutral975, 0.38),
      active: alpha(accent, 0.56)
    }
  } as ThemeOptions['palette'];
}

export function buildTheme(mode: ColorMode) {
  return initializeTheme({
    mode,
    overrides: {
      palette: paletteFor(mode),
      typography,
      shape: { borderRadius: 8 },
      components: {
        MuiButton: {
          defaultProps: { disableElevation: true },
          styleOverrides: {
            root: { fontSize: '0.9375rem', fontWeight: 600, lineHeight: '20px', textTransform: 'none' }
          }
        },
        MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }
      }
    }
  });
}

export const themes = { light: buildTheme('light'), dark: buildTheme('dark') };
