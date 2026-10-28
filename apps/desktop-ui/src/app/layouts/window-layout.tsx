import { Outlet, useRouteError } from 'react-router';

import { ErrorContent } from '@/shared/components';

import { AppShell } from './app-shell';

export function WindowLayout() {
  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}

/** Erro numa tela: a janela continua utilizável para a pessoa se recuperar. */
export function WindowLayoutError() {
  const error = useRouteError();

  return (
    <AppShell>
      <ErrorContent error={error} source="route-error" />
    </AppShell>
  );
}
