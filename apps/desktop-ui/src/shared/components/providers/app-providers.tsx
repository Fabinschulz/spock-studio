import { Provider } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';

import { theme } from './theme';

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <Provider theme={theme} cssBaseline={false}>
      {children}
    </Provider>
  );
}
