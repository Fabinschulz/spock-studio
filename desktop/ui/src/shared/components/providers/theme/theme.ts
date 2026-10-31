import { initializeTheme } from '@synthra.io/ui-kit';

import { components } from './components';
import { palette } from './palette';
import { typography } from './typography';

export const theme = initializeTheme({
  mode: 'dark',
  overrides: {
    // `brand` é um campo da Synthra fora da tipagem de PaletteOptions do MUI.
    palette: palette as never,
    typography,
    shape: { borderRadius: 6 },
    components
  }
});
