import { ErrorState } from '@/shared/components';
import { useEngineStatus } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';
import { useEffect, useRef, useState } from 'react';

import { restartEngine } from '../service/engine-restart-api';
import styles from './restart-engine.module.scss';

/**
 * A saída quando o motor parou e a tentativa automática já foi gasta. Só aparece com o motor parado; quando ele fica
 * pronto de novo, avisa quem precisa ler outra vez o que falhou. Se ele parar de novo, o botão volta.
 */
export function RestartEngine({ onReady }: { onReady?: () => void }) {
  const engine = useEngineStatus();
  const [asking, setAsking] = useState(false);
  const awaitingReady = useRef(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!awaitingReady.current || engine.status !== 'ready') return;
    awaitingReady.current = false;
    onReady?.();
  }, [engine.status, onReady]);

  if (!engine.hosted || engine.status !== 'crashed') return null;

  const restart = async () => {
    setFailed(false);
    setAsking(true);
    try {
      awaitingReady.current = true;
      await restartEngine();
    } catch {
      awaitingReady.current = false;
      setFailed(true);
    } finally {
      setAsking(false);
    }
  };

  return (
    <div className={styles.restart}>
      <Button type="button" variant="outlined" disabled={asking} onClick={() => void restart()}>
        Reiniciar o motor
      </Button>
      {failed ? (
        <ErrorState>
          Não foi possível reiniciar o motor. Feche e abra o aplicativo; se continuar, copie as informações do
          Diagnóstico para o suporte.
        </ErrorState>
      ) : null}
    </div>
  );
}
