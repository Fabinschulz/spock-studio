import { ConfirmGate, DiffView, SuccessState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { IacGeneratePlan } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';

type ReviewedChange = IacGeneratePlan['changes'][number];

/** O diff de cada arquivo que o motor montou, na ordem dele. */
export function ReviewedChanges({ changes }: { changes: ReviewedChange[] }) {
  return changes.map((change) => (
    <div key={change.path}>
      <p className={styles.hint}>
        {change.path} · {change.op === 'create' ? 'arquivo novo' : 'alterado'}
      </p>
      <DiffView diff={change.diff} label={change.path} />
    </div>
  ));
}

interface SaveReviewedFilesProps {
  /** O que vai ser gravado, para o nome da confirmação: "os arquivos do bootstrap". */
  what: string;
  busy: boolean;
  /** Pode gravar: com verificação falhando, só o caminho de volta aparece. */
  canSave?: boolean;
  onSave: () => void;
  onBack: () => void;
  backLabel: string;
}

/** Gravar no projeto passa por uma confirmação que diz o que muda: só arquivos, nada na nuvem. */
export function SaveReviewedFiles({ what, busy, canSave = true, onSave, onBack, backLabel }: SaveReviewedFilesProps) {
  return (
    <ConfirmGate
      label={`Gravar ${what}`}
      confirmLabel="Gravar"
      busy={busy}
      message="Só arquivos vão para o projeto; nada é criado na nuvem agora."
      onConfirm={onSave}
    >
      {(ask) => (
        <div className={styles.actions}>
          {canSave ? (
            <Button type="button" disabled={busy} onClick={ask}>
              Gravar no projeto
            </Button>
          ) : null}
          <Button type="button" variant="outlined" disabled={busy} onClick={onBack}>
            {backLabel}
          </Button>
        </div>
      )}
    </ConfirmGate>
  );
}

/** A gravação virou uma execução do Spock: é por ela que se desfaz. */
export function SavedFiles({ runId, what, next }: { runId: string; what: string; next: string }) {
  return (
    <SuccessState>
      {what} na execução {runId}, em <Link to={pathTo('runs')}>Execuções</Link> (dá para desfazer por lá). Próximo
      passo: {next}
    </SuccessState>
  );
}
