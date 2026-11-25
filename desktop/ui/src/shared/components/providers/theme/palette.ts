import { alpha } from '@mui/material/styles';

import { brandPalette as c, veil } from './brand-palette';

export const palette = {
  primary: { light: c.teal300, main: c.teal400, dark: c.teal300, contrastText: c.onPrimary },
  secondary: { light: c.teal300, main: c.teal400, dark: c.teal300, contrastText: c.onPrimary },
  background: { default: c.background, paper: c.surface },
  text: { primary: c.text, secondary: c.textMuted },
  divider: c.line,
  error: { main: c.error },
  success: { main: c.success },
  warning: { main: c.warning },
  info: { main: c.info },
  brand: {
    lightest: alpha(c.teal400, 0.12),
    light: c.teal300,
    medium: c.teal400,
    dark: c.teal400,
    darkest: c.teal400
  },
  action: {
    hover: veil.hover,
    selected: veil.selected,
    focus: alpha(c.teal400, 0.16),
    disabledBackground: veil.disabledFill,
    disabled: veil.disabledText,
    active: veil.activeIcon
  }
};
