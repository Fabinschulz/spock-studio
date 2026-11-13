import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';
import { DEFAULT_ENVIRONMENT, environmentOf, isEnvironment, type Environment } from './environment';

const STORAGE_KEY = 'spock.environment';

// O armazenamento do navegador pode faltar ou recusar (janela privada, dados bloqueados): a preferência é só conforto.
function readRemembered(): Environment {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return isEnvironment(value) ? value : DEFAULT_ENVIRONMENT;
  } catch {
    return DEFAULT_ENVIRONMENT;
  }
}

function remember(environment: Environment): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, environment);
  } catch {
    // Sem armazenamento, o ambiente só dura até fechar o aplicativo.
  }
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

  useEffect(() => remember(environment), [environment]);

  return environment;
}
