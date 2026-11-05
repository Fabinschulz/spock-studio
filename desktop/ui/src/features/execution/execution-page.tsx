import { errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice, StatusChip, type Tone } from '@/shared/components/ui';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import { runStateLabel } from '@/shared/utils';

import { EvaluationSection } from './components/evaluation-section';
import { EvidenceList } from './components/evidence-list';
import { ProposedActions } from './components/proposed-actions';
import { StageList } from './components/stage-list';
import styles from './components/workspace.module.scss';
import { describeEvent, pendingApproval } from './domain/run-timeline';
import { deriveEvidence, deriveStages, failureHint, riskLabel } from './domain/run-workspace';
import { useRunFollow } from './hooks/use-run-follow';
import { resumeWorkflow } from './service/execution-api';

export { workflowSnapshotSchema, type WorkflowSnapshot } from './domain/run-timeline';

const ACTION_STATUS_LABEL: Record<string, string> = {
  executed: 'executada',
  skipped: 'não aprovada, nada foi gravado',
  conflict: 'não gravada: já existe um arquivo diferente neste caminho',
  failed: 'não foi possível executar',
  approved: 'aprovada, aguardando a execução',
  proposed: 'proposta'
};

const AREA_LABEL: Record<string, string> = {
  servico: 'Serviço',
  interface: 'Interface',
  projeto: 'Projeto'
};

const STATE_TONE: Record<string, Tone> = {
  completed: 'success',
  completed_with_limitations: 'warning',
  failed: 'danger',
  requires_intervention: 'warning',
  awaiting_approval: 'info'
};

export function ExecutionPage({ runId, client }: { runId: string; client: EngineClient }) {
  const { view, error, setError, refresh } = useRunFollow(client, runId);
  const [busy, setBusy] = useState(false);
  // Nada vem marcado: executar uma ação é uma escolha explícita de quem aprova.
  const [chosen, setChosen] = useState<readonly string[]>([]);

  const approve = async () => {
    if (!view) return;
    setBusy(true);
    setError(null);
    try {
      const pending = pendingApproval(view.events);
      await resumeWorkflow(
        client,
        view.snapshot.projectId,
        runId,
        pending
          ? {
              approvalId: pending.approvalId,
              actionIds: chosen.filter((id) => pending.actions.some((a) => a.id === id))
            }
          : undefined
      );
      await refresh();
    } catch (cause) {
      setError(errorMessage(cause, 'Não foi possível retomar a execução.'));
    } finally {
      setBusy(false);
    }
  };

  if (error && !view) return <p role="alert">{error}</p>;
  if (!view) return <p role="status">Lendo a execução…</p>;

  const { snapshot } = view;
  const result = snapshot.result;
  const stateLabel = runStateLabel(snapshot.state);
  const pending = snapshot.state === 'awaiting_approval' ? pendingApproval(view.events) : null;
  const toggle = (id: string) =>
    setChosen((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  const timeline = view.events.flatMap((event) => {
    const text = describeEvent(event);
    return text ? [{ seq: event.seq, text }] : [];
  });
  const stages = deriveStages(view.events, snapshot.state);
  const evidence = deriveEvidence(view.events, result);
  const showEvidence = result !== null || evidence.length > 0;

  return (
    <section className={styles.page} aria-labelledby="execucao-titulo">
      <h1 id="execucao-titulo">Execução</h1>
      <p role="status">
        Estado: <StatusChip tone={STATE_TONE[snapshot.state] ?? 'neutral'}>{stateLabel}</StatusChip>
      </p>
      {result?.summary ? <p>{result.summary}</p> : null}
      {error ? <p role="alert">{error}</p> : null}

      {result?.error ? (
        <InlineNotice tone="danger" title="A execução falhou">
          <p className={styles.noticeText}>{result.error.message}</p>
          {failureHint(result.error.code) ? (
            <p className={styles.noticeText}>{failureHint(result.error.code)}</p>
          ) : null}
          <p className={styles.noticeText}>
            Código: <span className={styles.mono}>{result.error.code}</span>
          </p>
        </InlineNotice>
      ) : null}

      {stages.length > 0 ? (
        <section aria-labelledby="etapas-titulo">
          <h2 id="etapas-titulo">Etapas</h2>
          <StageList stages={stages} outcome={stateLabel} />
        </section>
      ) : null}

      {timeline.length > 0 ? (
        <section aria-labelledby="linha-titulo">
          <h2 id="linha-titulo">O que aconteceu</h2>
          <ol className={styles.list} aria-live="polite">
            {timeline.map((item) => (
              <li key={item.seq} className={styles.row}>
                {item.text}
              </li>
            ))}
          </ol>
        </section>
      ) : null}

      {result?.impact ? (
        <section aria-labelledby="impacto-titulo">
          <h2 id="impacto-titulo">Impacto técnico</h2>
          <ul className={styles.list}>
            {result.impact.areas.map((area) => (
              <li key={area}>{AREA_LABEL[area] ?? area}</li>
            ))}
          </ul>
          {result.impact.risks.length > 0 ? (
            <>
              <h3 className={styles.subtitle}>Riscos</h3>
              <ul className={styles.list}>
                {result.impact.risks.map((risk) => (
                  <li key={risk}>{riskLabel(risk)}</li>
                ))}
              </ul>
            </>
          ) : null}
        </section>
      ) : null}

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

      {result?.usage ? (
        <section aria-labelledby="modelo-titulo">
          <h2 id="modelo-titulo">Uso do modelo</h2>
          <p>
            {result.usage.modelCalls} chamada(s) · {result.usage.inputTokens} tokens de entrada ·{' '}
            {result.usage.outputTokens} de saída. Contado pelo motor.
          </p>
        </section>
      ) : null}

      {result && result.limitations.length > 0 ? (
        <section aria-labelledby="limites-titulo">
          <h2 id="limites-titulo">Limitações</h2>
          <ul className={styles.list}>
            {result.limitations.map((item) => (
              <li key={item.code}>{item.message}</li>
            ))}
          </ul>
        </section>
      ) : null}

      {pending && pending.actions.length > 0 ? (
        <ProposedActions pending={pending} chosen={chosen} busy={busy} onToggle={toggle} />
      ) : null}

      {result?.actions && result.actions.length > 0 ? (
        <section aria-labelledby="acoes-resultado-titulo">
          <h2 id="acoes-resultado-titulo">Ações</h2>
          <ul className={styles.list}>
            {result.actions.map((action) => (
              <li key={action.id} className={styles.row}>
                {action.summary}: {ACTION_STATUS_LABEL[action.status] ?? action.status}
                {action.target ? <span className={styles.path}> {action.target}</span> : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {snapshot.state === 'awaiting_approval' ? (
        <Button type="button" onClick={() => void approve()} disabled={busy}>
          {busy
            ? 'Retomando…'
            : pending && pending.actions.length > 0
              ? chosen.length > 0
                ? 'Aprovar e executar as ações marcadas'
                : 'Aprovar sem executar ações'
              : 'Aprovar'}
        </Button>
      ) : null}
    </section>
  );
}
