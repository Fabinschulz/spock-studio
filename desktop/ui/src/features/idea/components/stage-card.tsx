import { InlineNotice, PageSection, StatusChip } from '@/shared/components';
import type { IdeaDraft, IdeaStage } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';

import styles from '../idea-page.module.scss';
import { ArtifactView } from './artifact-view';

export const STAGE_LABELS: Record<IdeaStage, string> = {
  specification: 'Especificação',
  architecture: 'Arquitetura',
  backlog: 'Backlog'
};

interface StageCardProps {
  index: number;
  draft: IdeaDraft;
  approved: boolean;
  busy: boolean;
  onApprove: () => void;
  onRetry: () => void;
}

export function StageCard({ index, draft, approved, busy, onApprove, onRetry }: StageCardProps) {
  const chip: { tone: 'success' | 'info' | 'danger'; label: string } = approved
    ? { tone: 'success', label: 'Aprovada' }
    : draft.valid
      ? { tone: 'info', label: 'Passou nas verificações' }
      : { tone: 'danger', label: 'Não passou' };

  return (
    <PageSection
      title={`${index}. ${STAGE_LABELS[draft.stage]}`}
      action={<StatusChip tone={chip.tone}>{chip.label}</StatusChip>}
    >
      <ul className={styles.checks} aria-label="Verificações do motor">
        {draft.checks.map((check) => (
          <li key={check.id}>
            {check.passed ? 'Passou' : 'Falhou'}: {check.label}
            {check.detail ? ` (${check.detail})` : ''}
          </li>
        ))}
      </ul>
      {draft.valid ? null : (
        <InlineNotice tone="warning" title="O modelo não acertou esta etapa">
          Mesmo depois de receber o que faltava, a resposta não passou em todas as verificações. Gere de novo; se
          insistir, detalhe mais a ideia.
        </InlineNotice>
      )}
      {draft.artifact ? <ArtifactView stage={draft.stage} artifact={draft.artifact} /> : null}
      <p className={styles.note}>
        {draft.attempts === 1 ? 'Gerada em 1 tentativa' : `Gerada em ${draft.attempts} tentativas`},{' '}
        {(draft.inputTokens + draft.outputTokens).toLocaleString('pt-BR')} tokens.
      </p>
      {approved ? null : (
        <div className={styles.actions}>
          {draft.valid ? (
            <Button type="button" variant="contained" disabled={busy} onClick={onApprove}>
              Aprovar e seguir
            </Button>
          ) : null}
          <Button type="button" variant="outlined" color="inherit" disabled={busy} onClick={onRetry}>
            Gerar de novo
          </Button>
        </div>
      )}
    </PageSection>
  );
}
