import type { ThemeOptions } from '@mui/material/styles';

import { brandPalette as c } from './brand-palette';

const SUBTLE_HOVER = 'rgba(160, 200, 196, 0.08)';

export const components: ThemeOptions['components'] = {
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
            '&:hover': { borderColor: c.textMuted, backgroundColor: SUBTLE_HOVER }
          }),
        ...(ownerState.color === 'inherit' &&
          ownerState.variant === 'text' && {
            color: c.textMuted,
            '&:hover': { backgroundColor: SUBTLE_HOVER, color: c.text }
          })
      })
    }
  },
  MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }
};
