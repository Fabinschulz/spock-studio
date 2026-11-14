import { EmptyState, PageSection, ResourceView } from '@/shared/components';
import type { Resource } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';

import type { LogLine } from '../domain/diagnostic-log';
import styles from './diagnostic-log-section.module.scss';

const SHOWN = 50;

const timeOf = (atMs: number) => new Date(atMs).toLocaleTimeString('pt-BR');

export function LogList({ lines }: { lines: readonly LogLine[] }) {
  if (lines.length === 0) return <EmptyState>Nenhuma linha ainda.</EmptyState>;

  return (
    <ol className={styles.log}>
      {[...lines]
        .reverse()
        .slice(0, SHOWN)
        .map((line) => (
          <li key={`${line.atMs}-${line.source}-${line.message}`} className={styles.line}>
            <span className={styles.time}>{timeOf(line.atMs)}</span>
            <span className={styles.source}>{line.source}</span>
            <span className={styles.message}>
              {line.message}
              {line.count > 1 ? ` (×${line.count})` : ''}
            </span>
          </li>
        ))}
    </ol>
  );
}

export function DiagnosticLogSection({ log, hosted }: { log: Resource<LogLine[]>; hosted: boolean }) {
  return (
    <PageSection
      title="Registro recente"
      description="As últimas linhas do motor e do aplicativo. Chaves e caminhos de pasta são trocados por marcadores antes de aparecer aqui."
      action={
        hosted ? (
          <Button variant="outlined" onClick={log.reload}>
            Atualizar
          </Button>
        ) : undefined
      }
    >
      {hosted ? (
        <ResourceView resource={log} loading="Lendo o registro…" fallbackError="Não foi possível ler o registro.">
          {(lines) => <LogList lines={lines} />}
        </ResourceView>
      ) : (
        <EmptyState>Sem motor local, não há registro nesta prévia.</EmptyState>
      )}
    </PageSection>
  );
}
