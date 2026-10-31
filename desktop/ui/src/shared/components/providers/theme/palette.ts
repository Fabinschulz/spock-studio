import { alpha } from '@mui/material/styles';

import { brandPalette as c } from './brand-palette';

export const palette = {
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
