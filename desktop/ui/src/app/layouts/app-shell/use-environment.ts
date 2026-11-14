import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';
import { DEFAULT_ENVIRONMENT, environmentOf, isEnvironment, type Environment } from './environment';
import { readPreference, writePreference } from './preference';

const STORAGE_KEY = 'spock.environment';

function readRemembered(): Environment {
  const value = readPreference(STORAGE_KEY);
  return isEnvironment(value) ? value : DEFAULT_ENVIRONMENT;
}

/** Nas telas que não pertencem a um ambiente vale o último em que a pessoa esteve, guardado para a próxima abertura. */
export function useEnvironment(): Environment {
  const { pathname } = useLocation();
  const fromRoute = environmentOf(pathname);
  const [remembered, setRemembered] = useState<Environment>(readRemembered);

  // O ambiente lembrado acompanha a rota durante a renderização, sem passar por um efeito: assim a tela nunca aparece
  // por um instante com o menu do ambiente anterior.
  if (fromRoute && fromRoute !== remembered) setRemembered(fromRoute);
  const environment = fromRoute ?? remembered;

  useEffect(() => writePreference(STORAGE_KEY, environment), [environment]);

  return environment;
}
