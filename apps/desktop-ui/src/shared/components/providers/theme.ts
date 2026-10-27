import { alpha, type ThemeOptions } from '@mui/material/styles';
import { initializeTheme } from '@synthra.io/ui-kit';

import { brandPalette as c } from './brand-palette';

export type ColorMode = 'light' | 'dark';

const FONT_SANS = "'Geist', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif";

/** A Synthra calcula as variantes com Lato antes dos overrides; a fonte é trocada variante a variante. */
const VARIANTS = [
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'subtitle1', 'subtitle2', 'body1', 'body2',
  'button', 'caption', 'overline', 'xg', 'xxxl', 'xxl'
] as const; // prettier-ignore

const typography = {
  fontFamily: FONT_SANS,
  ...Object.fromEntries(VARIANTS.map((variant) => [variant, { fontFamily: FONT_SANS }]))
};

/** A Synthra não recalcula tons derivados: todo tom usado pelos componentes é declarado aqui. */
function paletteFor(mode: ColorMode): ThemeOptions['palette'] {
  const dark = mode === 'dark';
  const accent = dark ? c.teal400 : c.teal700;
  return {
    primary: dark
      ? { light: c.teal300, main: c.teal400, dark: c.teal500, contrastText: c.neutral975 }
      : { light: c.teal400, main: c.teal700, dark: c.teal800, contrastText: c.white },
    secondary: dark
      ? { light: c.teal300, main: c.teal400, dark: c.teal500, contrastText: c.neutral975 }
      : { light: c.teal400, main: c.teal700, dark: c.teal800, contrastText: c.white },
    background: dark ? { default: c.neutral975, paper: c.neutral950 } : { default: c.white, paper: c.white },
    text: dark ? { primary: c.neutral50, secondary: c.neutral350 } : { primary: c.neutral950, secondary: c.neutral600 },
    divider: dark ? c.neutral800 : c.neutral200,
    error: { main: dark ? c.errorDark : c.errorLight },
    success: { main: dark ? c.successDark : c.successLight },
    brand: {
      lightest: alpha(c.teal400, 0.12),
      light: c.teal300,
      medium: c.teal400,
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
        MuiButton: { defaultProps: { disableElevation: true } },
        MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }
      }
    }
  });
}

export const themes = { light: buildTheme('light'), dark: buildTheme('dark') };
