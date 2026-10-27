import { Alert, Button, ReloadIcon } from '@synthra.io/ui-kit';
import { useEffect } from 'react';

import styles from './error-content.module.scss';

export interface ErrorContentProps {
  error: unknown;
  /** Identifica o boundary que capturou, para o diagnóstico. */
  source: string;
}

export function ErrorContent({ error, source }: ErrorContentProps) {
  useEffect(() => {
    // O objeto de erro pode conter detalhes internos: só o nome e a origem vão para o console.
    console.error(`[${source}]`, error instanceof Error ? error.name : 'erro desconhecido');
  }, [error, source]);

  return (
    <div className={styles.content}>
      <Alert
        severity="error"
        title="Algo não saiu como esperado"
        description="Tivemos um problema ao exibir esta tela. Tente novamente e, se o problema persistir, reinicie o aplicativo."
      />
      <Button variant="contained" startIcon={<ReloadIcon />} onClick={() => window.location.reload()}>
        Tentar novamente
      </Button>
    </div>
  );
}
