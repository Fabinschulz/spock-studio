import type { EngineStatus as Status } from '@/shared/services';
import { cn } from '@/shared/utils';

import styles from './AppShell.module.scss';

export type Tone = 'ok' | 'pending' | 'bad' | 'idle';

/** O CSS Modules entrega os nomes em camelCase: a classe do ponto sai de um mapa, não de um nome montado em texto. */
export const dotClass: Record<Tone, string | undefined> = {
  ok: styles.dotOk,
  pending: styles.dotPending,
  bad: styles.dotBad,
  idle: styles.dotIdle
};

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

export function EngineStatus({ status, hosted }: { status: Status; hosted: boolean }) {
  const { title, detail, tone } = describe(status, hosted);

  return (
    <div className={styles.engine} role="status" aria-live="polite">
      <span className={cn(styles.dot, dotClass[tone])} aria-hidden="true" />
      <span className={styles.engineText}>
        <span className={styles.engineTitle}>{title}</span>
        <span className={styles.engineDetail}>{detail}</span>
      </span>
    </div>
  );
}
