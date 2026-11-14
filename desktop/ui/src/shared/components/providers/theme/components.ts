import type { ThemeOptions } from '@mui/material/styles';

import { brandPalette as c, overlay, veil } from './brand-palette';
import { RouterLinkBehavior } from './router-link';

const overlaySurface = {
  backgroundColor: overlay.background,
  backdropFilter: overlay.backdrop,
  '@media (prefers-reduced-transparency: reduce)': { backgroundColor: c.raised, backdropFilter: 'none' }
};

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
  MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
  MuiDialog: { styleOverrides: { paper: overlaySurface } },
  MuiPopover: { styleOverrides: { paper: overlaySurface } },
  MuiTooltip: {
    styleOverrides: {
      tooltip: {
        ...overlaySurface,
        padding: '4px 8px',
        border: `1px solid ${c.line}`,
        color: c.text,
        fontSize: '0.75rem',
        fontWeight: 500
      }
    }
  }
};
