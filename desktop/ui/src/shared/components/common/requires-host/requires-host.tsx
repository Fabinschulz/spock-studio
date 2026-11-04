import { isTauriRuntime } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import { Alert } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './requires-host.module.scss';

interface RequiresHostProps {
  children: ReactNode;
}

/**
 * Telas que só funcionam com o motor local. No navegador (prévia), em vez de um erro genérico na primeira chamada,
 * diz o motivo e oferece o caminho de volta.
 */
export function RequiresHost({ children }: RequiresHostProps) {
  if (isTauriRuntime()) return children;

  return (
    <div className={styles.content}>
      <Alert
        severity="info"
        title="Disponível apenas no aplicativo desktop"
        description="Esta tela depende do motor local, que só roda dentro do aplicativo Mariliz Studio."
      />
      <Link to={pathTo('home')}>Voltar para o início</Link>
    </div>
  );
}
