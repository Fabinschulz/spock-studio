import { Button, LoadingBar } from '@mariliz/ui-kit';
import { useState } from 'react';
import { Link } from 'react-router';

import { ConfirmAction } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import { formatEngineDate } from '@/shared/utils';

import styles from '../../components/panels.module.scss';
import { aiEnvironmentNames } from '../../domain/ai-environments';
import { componentsSummary, type ConfiguredProject } from '../../domain/configured-project';
import { runKindLabel } from '../../domain/labels';
import type { RunView } from '../../types/run';

interface ConfiguredSummaryProps {
  configured: ConfiguredProject;
  /** A execução em andamento, se houver: os atalhos esperam ela terminar. */
  run: RunView | null;
  onValidate: () => void;
  onRevert: (runId: string) => void;
}

/** Segunda visita: o estado do que já foi aplicado e os atalhos, sem passar pelo plano de novo. */
export function ConfiguredSummary({ configured, run, onValidate, onRevert }: ConfiguredSummaryProps) {
  const [confirming, setConfirming] = useState(false);
  const { status, revertible } = configured;
  const busy = run !== null && run.error === null;
  const details = [
    componentsSummary(status),
    status.aiEnvironment ? `ambiente de IA: ${aiEnvironmentNames[status.aiEnvironment] ?? status.aiEnvironment}` : null,
    revertible ? `última aplicação ${formatEngineDate(revertible.startedAt)}` : null
  ].filter(Boolean);

  return (
    <section aria-labelledby="configurado-titulo" className={styles.card}>
      <h3 id="configurado-titulo" className={styles.cardTitle}>
        Este projeto já está configurado
      </h3>
      <p className={styles.muted}>{details.join(' · ')}.</p>
      <p className={styles.muted}>
        O detalhe por componente está em <Link to={pathTo('projectHarness')}>Padrões do projeto</Link>. Para mudar a
        configuração, analise o projeto e gere um plano novo.
      </p>

      {busy ? (
        <div role="status" aria-live="polite" className={styles.stack}>
          <span>{runKindLabel[run.kind]} em andamento…</span>
          <LoadingBar aria-label={`${runKindLabel[run.kind]} em andamento`} />
        </div>
      ) : null}

      {confirming && revertible ? (
        <ConfirmAction
          label="Reverter a última aplicação"
          confirmLabel="Reverter agora"
          cancelLabel="Não reverter"
          onConfirm={() => {
            setConfirming(false);
            onRevert(revertible.runId);
          }}
          onCancel={() => setConfirming(false)}
        >
          Isto restaura o estado anterior dos arquivos que a última aplicação alterou. Arquivos que você editou depois
          dela não são sobrescritos: ficam como estão e aparecem como pendência.
        </ConfirmAction>
      ) : (
        <div className={styles.row}>
          <Button variant="outlined" color="inherit" size="small" disabled={busy} onClick={onValidate}>
            Validar de novo
          </Button>
          {revertible ? (
            <Button variant="outlined" color="inherit" size="small" disabled={busy} onClick={() => setConfirming(true)}>
              Reverter a última aplicação
            </Button>
          ) : null}
        </div>
      )}
    </section>
  );
}
