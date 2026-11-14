import type { ThemeOptions } from '@mui/material/styles';

import { brandPalette as c, veil } from './brand-palette';
import { RouterLinkBehavior } from './router-link';

export const components: ThemeOptions['components'] = {
  MuiLink: { defaultProps: { component: RouterLinkBehavior } },
  MuiButtonBase: { defaultProps: { LinkComponent: RouterLinkBehavior } },
  MuiButton: {
    defaultProps: { disableElevation: true },
    styleOverrides: {
      root: ({ ownerState }) => ({
        textTransform: 'none',
        minHeight: 34,
        ...(ownerState.color === 'inherit' &&
          ownerState.variant === 'outlined' && {
            borderColor: c.borderStrong,
            color: c.text,
            '&:hover': { borderColor: c.textMuted, backgroundColor: veil.hover }
          }),
        ...(ownerState.color === 'inherit' &&
          ownerState.variant === 'text' && {
            color: c.textMuted,
            '&:hover': { backgroundColor: veil.hover, color: c.text }
          })
      })
    }
  },
  MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }
};
