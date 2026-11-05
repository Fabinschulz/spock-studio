import { Provider } from '@mariliz/ui-kit';
import type { ReactNode } from 'react';

import { theme } from './theme';

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <Provider theme={theme} cssBaseline={false}>
      {children}
    </Provider>
  );
}
