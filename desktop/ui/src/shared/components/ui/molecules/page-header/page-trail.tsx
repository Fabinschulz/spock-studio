import { createContext, useContext } from 'react';

import type { Crumb } from './PageHeader';

const PageTrailContext = createContext<readonly Crumb[]>([]);

export const PageTrailProvider = PageTrailContext.Provider;

export function usePageTrail(): readonly Crumb[] {
  return useContext(PageTrailContext);
}
