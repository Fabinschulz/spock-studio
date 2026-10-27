import { useMediaQuery } from '@mui/material';
import { Provider } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';

import { themes } from './theme';

/** Tema da Synthra seguindo o sistema operacional. O reset global fica em shared/styles. */
export function AppProviders({ children }: { children: ReactNode }) {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)');

  return (
    <Provider theme={prefersDark ? themes.dark : themes.light} cssBaseline={false}>
      {children}
    </Provider>
  );
}
