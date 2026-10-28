import { useEngineStatus, type EngineStatus as Status } from '@/shared/services';
import { cn } from '@/shared/utils';

import styles from './AppShell.module.scss';

type Tone = 'ok' | 'pending' | 'bad' | 'idle';

/** Texto do estado, sempre a partir do que o host informa: nada aparece como "pronto" sem o motor ter respondido. */
function describe(status: Status, hosted: boolean): { title: string; detail: string; tone: Tone } {
  if (!hosted) return { title: 'Prévia no navegador', detail: 'Sem motor local', tone: 'idle' };
  switch (status) {
    case 'ready':
      return { title: 'Sistema local', detail: 'Pronto para usar', tone: 'ok' };
    case 'starting':
      return { title: 'Sistema local', detail: 'Iniciando o motor…', tone: 'pending' };
    case 'crashed':
      return { title: 'Sistema local', detail: 'Motor indisponível. Reinicie o aplicativo.', tone: 'bad' };
    default:
      return { title: 'Sistema local', detail: 'Verificando o motor…', tone: 'pending' };
  }
}

export function EngineStatus() {
  const { status, hosted } = useEngineStatus();
  const { title, detail, tone } = describe(status, hosted);

  return (
    <div className={styles.engine} role="status" aria-live="polite">
      <span className={cn(styles.dot, styles[`dot-${tone}`])} aria-hidden="true" />
      <span className={styles.engineText}>
        <span className={styles.engineTitle}>{title}</span>
        <span className={styles.engineDetail}>{detail}</span>
      </span>
    </div>
  );
}
