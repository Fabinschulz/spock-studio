import { lighten, type ThemeOptions } from '@mui/material/styles';

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
          }),
        // O vermelho do tema escuro é claro: o texto branco do MUI fica em 2,8:1. Texto escuro passa de 6:1, e o hover
        // clareia em vez de escurecer para não perder esse contraste.
        ...(ownerState.color === 'error' &&
          ownerState.variant === 'contained' && {
            color: c.background,
            '&:hover': { backgroundColor: lighten(c.error, 0.15) }
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
