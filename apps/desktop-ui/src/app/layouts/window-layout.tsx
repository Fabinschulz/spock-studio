import { Outlet, useRouteError } from 'react-router';

import { Container, ErrorContent } from '@/shared/components';

import styles from './window-layout.module.scss';

const MAIN_CONTENT_ID = 'conteudo';

export function WindowLayout() {
  return (
    <main id={MAIN_CONTENT_ID} className={styles.window}>
      <Container>
        <Outlet />
      </Container>
    </main>
  );
}

/** Erro numa tela: a janela continua utilizável para a pessoa se recuperar. */
export function WindowLayoutError() {
  const error = useRouteError();

  return (
    <main id={MAIN_CONTENT_ID} className={styles.window}>
      <Container>
        <ErrorContent error={error} source="route-error" />
      </Container>
    </main>
  );
}
