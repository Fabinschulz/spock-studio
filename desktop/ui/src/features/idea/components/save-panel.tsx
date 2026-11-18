import { ConfirmAction, DiffView, InlineNotice, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { IdeaPlan, IdeaSaveResult } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';
import { Link } from 'react-router';

import styles from '../idea-page.module.scss';

interface SavePanelProps {
  plan: IdeaPlan | null;
  saved: IdeaSaveResult | null;
  reverted: boolean;
  busy: boolean;
  onReview: () => void;
  onSave: (plan: IdeaPlan) => void;
  onCancel: () => void;
  onRevert: (saved: IdeaSaveResult) => void;
}

export function SavePanel({ plan, saved, reverted, busy, onReview, onSave, onCancel, onRevert }: SavePanelProps) {
  const [confirming, setConfirming] = useState<'save' | 'revert' | null>(null);

  if (saved) {
    return (
      <InlineNotice tone="success" title="Documentos gravados">
        A especificação, a arquitetura e o backlog estão na pasta arquitetura do Spock, e a gravação ficou em{' '}
        <Link to={pathTo('runs')}>Execuções</Link>. Próximo passo:{' '}
        <Link to={pathTo('configure')}>configurar o Harness</Link> do projeto, para que as ferramentas de IA trabalhem
        seguindo estes documentos.
        <div className={styles.actions}>
          {confirming === 'revert' ? (
            <ConfirmAction
              label="Desfazer a gravação dos documentos"
              confirmLabel="Desfazer"
              busy={busy}
              onConfirm={() => onRevert(saved)}
              onCancel={() => setConfirming(null)}
            >
              Os três documentos saem do projeto. As etapas aprovadas continuam nesta tela.
            </ConfirmAction>
          ) : (
            <Button type="button" variant="outlined" color="inherit" onClick={() => setConfirming('revert')}>
              Desfazer gravação
            </Button>
          )}
        </div>
      </InlineNotice>
    );
  }

  if (!plan) {
    return (
      <PageSection title="Gravar no projeto">
        {reverted ? <p className={styles.note}>A gravação anterior foi desfeita.</p> : null}
        <p className={styles.note}>As três etapas estão aprovadas. Revise os documentos antes de gravar.</p>
        <div className={styles.actions}>
          <Button type="button" variant="contained" disabled={busy} onClick={onReview}>
            Revisar os documentos
          </Button>
        </div>
      </PageSection>
    );
  }

  return (
    <PageSection title="Revisar os documentos">
      <p className={styles.note}>Estes arquivos vão para o projeto exatamente como abaixo.</p>
      {plan.changes.map((change) => (
        <div key={change.path} className={styles.change}>
          <h3 className={styles.changeTitle}>
            {change.path} · {change.op === 'create' ? 'arquivo novo' : 'alterado'}
          </h3>
          <DiffView diff={change.diff} label={change.path} />
        </div>
      ))}
      {confirming === 'save' ? (
        <ConfirmAction
          label="Gravar os documentos no projeto"
          confirmLabel="Gravar"
          busy={busy}
          onConfirm={() => onSave(plan)}
          onCancel={() => setConfirming(null)}
        >
          Uma cópia do que existir antes fica guardada, e dá para desfazer depois.
        </ConfirmAction>
      ) : (
        <div className={styles.actions}>
          <Button type="button" variant="contained" disabled={busy} onClick={() => setConfirming('save')}>
            Gravar documentos
          </Button>
          <Button type="button" variant="outlined" color="inherit" disabled={busy} onClick={onCancel}>
            Agora não
          </Button>
        </div>
      )}
    </PageSection>
  );
}
