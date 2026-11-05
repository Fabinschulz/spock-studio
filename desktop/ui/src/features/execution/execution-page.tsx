import { errorMessage, type EngineClient } from '@/shared/api';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import { runStateLabel } from '@/shared/utils';

import { describeEvent } from './domain/run-timeline';
import styles from './execution-page.module.scss';
import { useRunFollow } from './hooks/use-run-follow';
import { resumeWorkflow } from './service/execution-api';

export { workflowSnapshotSchema, type WorkflowSnapshot } from './domain/run-timeline';

/** Nota mínima por critério (critérios versão 1); abaixo dela a opinião é "reprovada". Igual a PASSING_SCORE no motor. */
const PASSING_SCORE = 3;
const CRITERION_LABEL: Record<string, string> = {
  plan_quality: 'Qualidade do plano',
  context_fit: 'Uso do conteúdo do projeto',
  request_fit: 'Atendimento ao pedido',
  architecture_coherence: 'Coerência arquitetural'
};

const AREA_LABEL: Record<string, string> = {
  servico: 'Serviço',
  interface: 'Interface',
  projeto: 'Projeto'
};

export function ExecutionPage({ runId, client }: { runId: string; client: EngineClient }) {
  const { view, error, setError, refresh } = useRunFollow(client, runId);
  const [busy, setBusy] = useState(false);

  const approve = async () => {
    if (!view) return;
    setBusy(true);
    setError(null);
    try {
      await resumeWorkflow(client, view.snapshot.projectId, runId);
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
  const stateLabel = runStateLabel(snapshot.state);
  const timeline = view.events.flatMap((event) => {
    const text = describeEvent(event);
    return text ? [{ seq: event.seq, text }] : [];
  });

  return (
    <section className={styles.page} aria-labelledby="execucao-titulo">
      <h1 id="execucao-titulo">Execução</h1>
      <p role="status">
        Estado: <strong>{stateLabel}</strong>
      </p>
      {snapshot.result?.summary ? <p>{snapshot.result.summary}</p> : null}
      {error ? <p role="alert">{error}</p> : null}

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

      {snapshot.result?.impact ? (
        <section aria-labelledby="impacto-titulo">
          <h2 id="impacto-titulo">Impacto técnico</h2>
          <ul className={styles.list}>
            {snapshot.result.impact.areas.map((area) => (
              <li key={area}>{AREA_LABEL[area] ?? area}</li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="evidencias-titulo">
        <h2 id="evidencias-titulo">Evidências</h2>
        {snapshot.result && snapshot.result.evidence.length > 0 ? (
          <ul className={styles.list}>
            {snapshot.result.evidence.map((item) => (
              <li key={item.chunkId} className={styles.row}>
                <span className={styles.path}>{item.filePath}</span>
                <p className={styles.excerpt}>{item.excerpt}</p>
              </li>
            ))}
          </ul>
        ) : (
          <p>Nenhuma evidência nesta execução.</p>
        )}
      </section>

      {snapshot.result?.evaluation?.judgment ? (
        <section aria-labelledby="avaliacao-titulo">
          <h2 id="avaliacao-titulo">Avaliação por modelo</h2>
          <p>
            Uma opinião registrada por um avaliador independente. Ela não decide o resultado da execução.{' '}
            <strong>
              {snapshot.result.evaluation.judge === 'passed'
                ? 'Todos os critérios alcançaram a nota mínima.'
                : 'Algum critério ficou abaixo da nota mínima.'}
            </strong>
          </p>
          <ul className={styles.list}>
            {Object.entries(snapshot.result.evaluation.judgment.scores).map(([criterion, score]) => (
              <li key={criterion}>
                {CRITERION_LABEL[criterion] ?? criterion}: {score} de 5
                {score < PASSING_SCORE ? ' (abaixo do mínimo)' : ''}
              </li>
            ))}
          </ul>
          <p>{snapshot.result.evaluation.judgment.rationale}</p>
          <p>
            Modelo: {snapshot.result.evaluation.judgment.model} ({snapshot.result.evaluation.judgment.modelVersion}),
            critérios versão {snapshot.result.evaluation.judgment.criteriaVersion}.
          </p>
        </section>
      ) : null}

      {snapshot.result?.usage ? (
        <section aria-labelledby="modelo-titulo">
          <h2 id="modelo-titulo">Uso do modelo</h2>
          <p>
            {snapshot.result.usage.modelCalls} chamada(s) · {snapshot.result.usage.inputTokens} tokens de entrada ·{' '}
            {snapshot.result.usage.outputTokens} de saída. Contado pelo motor.
          </p>
        </section>
      ) : null}

      {snapshot.result && snapshot.result.limitations.length > 0 ? (
        <section aria-labelledby="limites-titulo">
          <h2 id="limites-titulo">Limitações</h2>
          <ul className={styles.list}>
            {snapshot.result.limitations.map((item) => (
              <li key={item.code}>{item.message}</li>
            ))}
          </ul>
        </section>
      ) : null}

      {snapshot.state === 'awaiting_approval' ? (
        <Button type="button" onClick={() => void approve()} disabled={busy}>
          {busy ? 'Retomando…' : 'Aprovar'}
        </Button>
      ) : null}
    </section>
  );
}
