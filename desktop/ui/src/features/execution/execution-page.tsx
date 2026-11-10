import type { EngineClient } from '@/shared/api';
import { StatusChip } from '@/shared/components/ui';
import { runStateLabel } from '@/shared/utils';

import { ApproveButton } from './components/approve-button';
import { EvaluationSection } from './components/evaluation-section';
import { EvidenceList } from './components/evidence-list';
import { ProposedActions } from './components/proposed-actions';
import {
  ActionOutcomes,
  FailureNotice,
  ImpactSection,
  LimitationsSection,
  Timeline,
  UsageSection
} from './components/result-sections';
import { StageList } from './components/stage-list';
import styles from './components/workspace.module.scss';
import { STATE_TONE } from './domain/labels';
import { describeEvent, pendingApproval } from './domain/run-timeline';
import { deriveEvidence, deriveStages } from './domain/run-workspace';
import { useApproval } from './hooks/use-approval';
import { useRunFollow } from './hooks/use-run-follow';

export { workflowSnapshotSchema, type WorkflowSnapshot } from './domain/run-timeline';

export function ExecutionPage({ runId, client }: { runId: string; client: EngineClient }) {
  const { view, error, setError, refresh } = useRunFollow(client, runId);
  const { busy, chosen, toggle, approve } = useApproval(client, runId, view, refresh, setError);

  if (error && !view) return <p role="alert">{error}</p>;
  if (!view) return <p role="status">Lendo a execução…</p>;

  const { snapshot } = view;
  const result = snapshot.result;
  const stateLabel = runStateLabel(snapshot.state);
  const awaitingApproval = snapshot.state === 'awaiting_approval';
  const pending = awaitingApproval ? pendingApproval(view.events) : null;
  const timeline = view.events.flatMap((event) => {
    const text = describeEvent(event);
    return text ? [{ seq: event.seq, text }] : [];
  });
  const stages = deriveStages(view.events, snapshot.state);
  const evidence = deriveEvidence(view.events, result);
  const showEvidence = result !== null || evidence.length > 0;
  const hasActions = pending !== null && pending.actions.length > 0;

  return (
    <section className={styles.page} aria-labelledby="execucao-titulo">
      <h1 id="execucao-titulo">Execução</h1>
      <p role="status">
        Estado: <StatusChip tone={STATE_TONE[snapshot.state] ?? 'neutral'}>{stateLabel}</StatusChip>
      </p>
      {result?.summary ? <p>{result.summary}</p> : null}
      {error ? <p role="alert">{error}</p> : null}

      {result?.error ? <FailureNotice error={result.error} /> : null}

      {stages.length > 0 ? (
        <section aria-labelledby="etapas-titulo">
          <h2 id="etapas-titulo">Etapas</h2>
          <StageList stages={stages} outcome={stateLabel} />
        </section>
      ) : null}

      {timeline.length > 0 ? <Timeline items={timeline} /> : null}
      {result?.impact ? <ImpactSection impact={result.impact} /> : null}

      {showEvidence ? (
        <section aria-labelledby="evidencias-titulo">
          <h2 id="evidencias-titulo">Evidências</h2>
          {evidence.length > 0 ? (
            <>
              {result === null ? <p>O texto de cada trecho aparece quando a execução termina.</p> : null}
              <EvidenceList items={evidence} />
            </>
          ) : (
            <p>Nenhuma evidência nesta execução.</p>
          )}
        </section>
      ) : null}

      {result?.evaluation ? <EvaluationSection evaluation={result.evaluation} /> : null}
      {result?.usage ? <UsageSection usage={result.usage} /> : null}
      {result && result.limitations.length > 0 ? <LimitationsSection limitations={result.limitations} /> : null}
      {pending && hasActions ? (
        <ProposedActions pending={pending} chosen={chosen} busy={busy} onToggle={toggle} />
      ) : null}
      {result?.actions && result.actions.length > 0 ? <ActionOutcomes actions={result.actions} /> : null}

      {awaitingApproval ? (
        <ApproveButton
          busy={busy}
          hasActions={hasActions}
          chosenCount={chosen.length}
          onApprove={() => void approve()}
        />
      ) : null}
    </section>
  );
}
