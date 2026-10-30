import { useMediaQuery } from '@mui/material';
import { Provider } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';

import { themes } from './theme';

export function AppProviders({ children }: { children: ReactNode }) {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)');

  return (
    <Provider theme={prefersDark ? themes.dark : themes.light} cssBaseline={false}>
      {children}
    </Provider>
  );
}
