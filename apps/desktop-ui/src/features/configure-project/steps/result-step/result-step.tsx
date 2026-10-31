import { Alert, Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { EvidenceDisclosure, PrimaryActionFooter } from '@/shared/components/ui';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { JourneyResults, Loadable } from '../../types/journey';
import type { RunView } from '../../types/run';
import { outcomeLabel, runKindLabel } from '../../domain/labels';
import { awaitingHarnessProposal, explainOutcome, outcomeSeverity } from '../../domain/run-outcome';
import { FilesSummary } from './files-summary';
import { ProposalGuide } from './proposal-guide';
import { RevertConfirmModal } from './revert-confirm-modal';
import { RevertSummary } from './revert-summary';
import { ValidationTable } from './validation-table';

interface ResultStepProps {
  results: JourneyResults;
  /** Reversão ou validação em andamento. */
  run: RunView | null;
  onRevert: (runId: string) => void;
  onValidate: () => void;
  onDone: () => void;
  /** Gera o plano de novo, agora com a proposta do Harness Engineer (se a pessoa a escreveu). */
  onReplan: () => void;
  /** Estado do plano gerado a partir daqui; o erro dele precisa aparecer nesta etapa. */
  replan: Loadable<unknown>;
}

export function ResultStep({ results, run, onRevert, onValidate, onDone, onReplan, replan }: ResultStepProps) {
  const [confirming, setConfirming] = useState(false);
  const { apply, revert } = results;
  const busy = run !== null && run.error === null;
  const validation = results.validate?.validation ?? apply?.validation;
  const canRevert = apply?.rollbackAvailable === true && revert?.outcome !== 'completed';

  const confirmRevert = () => {
    setConfirming(false);
    if (apply) onRevert(apply.runId);
  };

  return (
    <section className={styles.step}>
      <StepHeader title="Resultado" />

      {apply ? (
        <Alert
          severity={outcomeSeverity[apply.outcome]}
          title={outcomeLabel[apply.outcome]}
          description={
            <>
              {explainOutcome(apply)}
              {apply.error ? (
                <div className={styles.mono}>
                  {apply.error.code}: {apply.error.message}
                </div>
              ) : null}
            </>
          }
        />
      ) : null}

      {apply?.files ? <FilesSummary files={apply.files} /> : null}

      {apply?.pending && apply.pending.length > 0 ? (
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Pendências e itens mantidos</h3>
          <ul className={styles.list}>
            {apply.pending.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {validation ? <ValidationTable report={validation} /> : null}

      {apply?.limitations && apply.limitations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Limitações do plano</h3>
          {apply.limitations.map((limitation) => (
            <Alert key={limitation.code + limitation.message} severity="warning" description={limitation.message} />
          ))}
        </>
      ) : null}

      {apply?.risks && apply.risks.length > 0 ? (
        <EvidenceDisclosure summary={`Riscos remanescentes (${apply.risks.length})`} items={apply.risks} />
      ) : null}

      {awaitingHarnessProposal(apply, revert) ? <ProposalGuide onReplan={onReplan} replan={replan} /> : null}

      {revert ? <RevertSummary result={revert} /> : null}

      {run?.error ? (
        <Alert severity="error" title={`${runKindLabel[run.kind]} não concluída`} description={run.error} />
      ) : null}

      {busy ? (
        <div role="status" aria-live="polite" className={styles.card}>
          <span>{runKindLabel[run.kind]} em andamento…</span>
          <div className={styles.progress} />
        </div>
      ) : null}

      <PrimaryActionFooter>
        {canRevert ? (
          <Button variant="outlined" color="inherit" disabled={busy} onClick={() => setConfirming(true)}>
            Reverter alterações
          </Button>
        ) : null}
        <Button variant="outlined" color="inherit" disabled={busy} onClick={onValidate}>
          Validar de novo
        </Button>
        <Button variant="contained" disabled={busy} onClick={onDone}>
          Concluir
        </Button>
      </PrimaryActionFooter>

      <RevertConfirmModal open={confirming} onCancel={() => setConfirming(false)} onConfirm={confirmRevert} />
    </section>
  );
}
