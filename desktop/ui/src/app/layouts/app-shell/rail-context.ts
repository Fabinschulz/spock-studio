import { createContext, useContext } from 'react';

/** Com o menu estreito os nomes saem da tela e cada item passa a mostrá-lo como dica ao lado. */
const RailCollapsedContext = createContext(false);

export const RailCollapsedProvider = RailCollapsedContext.Provider;

export const useRailCollapsed = () => useContext(RailCollapsedContext);
