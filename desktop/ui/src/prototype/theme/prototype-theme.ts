import { initializeTheme } from '@synthra.io/ui-kit';

const FONT = "'Inter', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif";

export const prototypeTheme = initializeTheme({
  mode: 'dark',
  overrides: {
    palette: {
      primary: { light: '#7fdcd6', main: '#33c1ba', dark: '#4fd3cc', contrastText: '#06201f' },
      secondary: { light: '#7fdcd6', main: '#33c1ba', dark: '#4fd3cc', contrastText: '#06201f' },
      background: { default: '#0a0f10', paper: '#0f1617' },
      text: { primary: '#e8f0ef', secondary: '#a5b5b4' },
      divider: 'rgba(160, 200, 196, 0.14)'
    },
    // O Button da Synthra lê `subtitle2` e `caption` nos overrides de tamanho; `styleOverrides.root` não altera o texto.
    typography: {
      fontFamily: FONT,
      subtitle2: { fontFamily: FONT, fontSize: '0.8125rem', fontWeight: 600, lineHeight: '20px' },
      caption: { fontFamily: FONT, fontSize: '0.75rem', fontWeight: 500, lineHeight: '16px' }
    },
    shape: { borderRadius: 6 },
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: ({ ownerState }) => ({
            fontSize: '0.8125rem',
            fontWeight: 600,
            lineHeight: '20px',
            textTransform: 'none',
            minHeight: 34,
            ...(ownerState.color === 'inherit' &&
              ownerState.variant === 'outlined' && {
                borderColor: '#5f7a7a',
                color: '#e8f0ef',
                '&:hover': { borderColor: '#a5b5b4', backgroundColor: 'rgba(160, 200, 196, 0.08)' }
              }),
            ...(ownerState.color === 'inherit' &&
              ownerState.variant === 'text' && {
                color: '#a5b5b4',
                '&:hover': { backgroundColor: 'rgba(160, 200, 196, 0.08)', color: '#e8f0ef' }
              })
          })
        }
      }
    }
  }
});
