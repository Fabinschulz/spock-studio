import type { AiRuntimeStatus, EngineStatus as Status } from '@/shared/hooks';
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
function describe(status: Status, aiRuntime: AiRuntimeStatus, hosted: boolean): { title: string; detail: string; tone: Tone } {
  if (!hosted) return { title: 'Prévia no navegador', detail: 'Sem motor local', tone: 'idle' };
  switch (status) {
    case 'ready':
      if (aiRuntime === 'unavailable')
        return { title: 'Sistema local', detail: 'Motor pronto. A inteligência local não está disponível.', tone: 'ok' };
      if (aiRuntime === 'stopped')
        return { title: 'Sistema local', detail: 'Motor pronto. A inteligência local está parada.', tone: 'ok' };
      return { title: 'Sistema local', detail: 'Pronto para usar', tone: 'ok' };
    case 'starting':
      return { title: 'Sistema local', detail: 'Iniciando o motor…', tone: 'pending' };
    case 'crashed':
      return { title: 'Sistema local', detail: 'Motor indisponível. Reinicie o aplicativo.', tone: 'bad' };
    default:
      return { title: 'Sistema local', detail: 'Verificando o motor…', tone: 'pending' };
  }
}

export function EngineStatus({
  status,
  hosted,
  aiRuntime = 'unknown'
}: {
  status: Status;
  hosted: boolean;
  aiRuntime?: AiRuntimeStatus;
}) {
  const { title, detail, tone } = describe(status, aiRuntime, hosted);

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
