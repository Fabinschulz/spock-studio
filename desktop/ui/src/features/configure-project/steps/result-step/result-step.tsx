import { Button, LoadingBar } from '@mariliz/ui-kit';
import { useState } from 'react';

import type { HarnessNames } from '@/shared/schema';
import { runOutcomeLabel } from '@/shared/utils';

import { ConfirmAction, EvidenceDisclosure, InlineNotice, PrimaryActionFooter, Term } from '@/shared/components/ui';

import styles from '../../components/panels.module.scss';
import { PlanError } from '../../components/plan-error';
import { StepHeader } from '../../components/step-header';
import type { JourneyResults, Loadable } from '../../types/journey';
import type { RunView } from '../../types/run';
import { runKindLabel } from '../../domain/labels';
import { explainOutcome, outcomeTone, resultExit } from '../../domain/run-outcome';
import { FilesSummary } from './files-summary';
import { ProposalGuide } from './proposal-guide';
import { RevertSummary } from './revert-summary';
import { ValidationList } from './validation-list';

interface ResultStepProps {
  results: JourneyResults;
  run: RunView | null;
  onRevert: (runId: string) => void;
  onValidate: () => void;
  onDone: () => void;
  onReplan: () => void;
  /** Estado do plano gerado a partir daqui; o erro dele precisa aparecer nesta etapa. */
  replan: Loadable<unknown>;
  naming: HarnessNames;
  /** O plano trazia o instalador da estrutura de especificação e ele ficou sem aprovação. */
  specificationSkipped?: boolean;
  onInstallSpecification?: () => void;
}

export function ResultStep({
  results,
  run,
  onRevert,
  onValidate,
  onDone,
  onReplan,
  replan,
  naming,
  specificationSkipped = false,
  onInstallSpecification = () => undefined
}: ResultStepProps) {
  const [confirming, setConfirming] = useState(false);
  const { apply, revert } = results;
  const busy = run !== null && run.error === null;
  const validation = results.validate?.validation ?? apply?.validation;
  const canRevert = apply?.rollbackAvailable === true && revert?.outcome !== 'completed';
  const exit = resultExit(apply, revert, specificationSkipped);
  const replanning = replan.status === 'loading';

  const confirmRevert = () => {
    setConfirming(false);
    if (apply) onRevert(apply.runId);
  };

  return (
    <section className={styles.step}>
      <StepHeader title="Resultado" />

      {apply ? (
        <InlineNotice tone={outcomeTone[apply.outcome]} title={runOutcomeLabel[apply.outcome]}>
          {explainOutcome(apply)}
          {apply.error ? <div>{apply.error.message}</div> : null}
          {exit === 'proposal' ? (
            <div className={styles.cause}>
              <p>
                Causa: o plano usou as <Term id="baseRules" />. Falta a proposta do <Term id="harnessEngineer" />; o
                próximo passo, abaixo, diz como pedir.
              </p>
            </div>
          ) : null}
          {exit !== null && specificationSkipped ? (
            <div className={styles.cause}>
              <p>
                {exit === 'proposal' ? 'Também: a' : 'Causa: a'} estrutura de especificação não foi instalada, porque o
                comando do instalador ficou sem aprovação. As verificações que dependem dela ficaram bloqueadas.
              </p>
              <Button
                variant={exit === 'install' ? 'contained' : 'outlined'}
                color={exit === 'install' ? 'primary' : 'inherit'}
                size="small"
                disabled={busy || replanning}
                onClick={onInstallSpecification}
              >
                {replanning ? 'Gerando plano…' : 'Instalar a estrutura e gerar o plano de novo'}
              </Button>
            </div>
          ) : null}
        </InlineNotice>
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

      {validation ? <ValidationList report={validation} /> : null}

      {apply?.limitations && apply.limitations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Limitações do plano</h3>
          {apply.limitations.map((limitation) => (
            <InlineNotice key={limitation.code + limitation.message} tone="warning">
              {limitation.message}
            </InlineNotice>
          ))}
        </>
      ) : null}

      {apply?.risks && apply.risks.length > 0 ? (
        <EvidenceDisclosure summary={`Riscos remanescentes (${apply.risks.length})`} items={apply.risks} />
      ) : null}

      {exit === 'proposal' ? <ProposalGuide onReplan={onReplan} replan={replan} busy={busy} naming={naming} /> : null}
      {exit === 'install' ? <PlanError plan={replan} /> : null}

      {revert ? <RevertSummary result={revert} /> : null}

      {run?.error ? (
        <InlineNotice tone="danger" title={`${runKindLabel[run.kind]} não concluída`}>
          {run.error}
        </InlineNotice>
      ) : null}

      {busy ? (
        <div role="status" aria-live="polite" className={styles.card}>
          <span>{runKindLabel[run.kind]} em andamento…</span>
          <LoadingBar aria-label={`${runKindLabel[run.kind]} em andamento`} />
        </div>
      ) : null}

      {confirming ? (
        <ConfirmAction
          label="Reverter as alterações"
          confirmLabel="Reverter agora"
          cancelLabel="Não reverter"
          onConfirm={confirmRevert}
          onCancel={() => setConfirming(false)}
        >
          Isto restaura o estado anterior dos arquivos que a aplicação alterou. Arquivos que você editou depois da
          aplicação não são sobrescritos: ficam como estão e aparecem como pendência.
        </ConfirmAction>
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
        <Button
          variant={exit === null ? 'contained' : 'outlined'}
          color={exit === null ? 'primary' : 'inherit'}
          disabled={busy}
          onClick={onDone}
        >
          Concluir
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
