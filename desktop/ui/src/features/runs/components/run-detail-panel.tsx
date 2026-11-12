import { formatEngineDate } from '@/features/integrations';
import type { EngineClient } from '@/shared/api';
import { InlineNotice, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { executionPath } from '@/shared/routing';
import type { RunDetail, RunItem } from '@/shared/schema';
import { Drawer } from '@mariliz/ui-kit';
import { useCallback, type ReactNode } from 'react';
import { Link } from 'react-router';

import { displayState, effectLabel, hasLiveView, nextStep, runKindLabel, stateTone } from '../domain/run-labels';
import { readRunDetail } from '../service/runs-api';
import styles from '../runs-page.module.scss';

interface RunDetailPanelProps {
  client: EngineClient;
  /** A execução aberta. `null` fecha o painel. */
  run: RunItem | null;
  onClose: () => void;
}

/**
 * O detalhe de uma execução num painel ao lado da lista. Só lê: nada aqui dispara, reverte nem valida; essas ações
 * continuam sendo explícitas nas jornadas que as têm.
 */
export function RunDetailPanel({ client, run, onClose }: RunDetailPanelProps) {
  return (
    <Drawer
      anchor="right"
      open={run !== null}
      onClose={onClose}
      title={run ? runKindLabel(run.kind) : undefined}
      description={run ? `${run.projectName} · ${formatEngineDate(run.startedAt)}` : undefined}
      closeLabel="Fechar o detalhe"
    >
      {run ? <Content key={run.runId} client={client} run={run} /> : null}
    </Drawer>
  );
}

function Content({ client, run }: { client: EngineClient; run: RunItem }) {
  const load = useCallback(() => readRunDetail(client, run.projectId, run.runId), [client, run.projectId, run.runId]);
  const detail = useAsyncResource(load);
  const guidance = nextStep(run);

  return (
    <div className={styles.detail}>
      <p className={styles.detailState}>
        <StatusChip tone={stateTone(run)}>{displayState(run)}</StatusChip>
        {effectLabel(run) ? <span className={styles.meta}> {effectLabel(run)}</span> : null}
      </p>
      {guidance ? <InlineNotice tone={run.state === 'failed' ? 'danger' : 'warning'}>{guidance}</InlineNotice> : null}
      {run.error ? (
        <DetailBlock title="Motivo">
          <p className={styles.error}>{run.error}</p>
        </DetailBlock>
      ) : null}
      {hasLiveView(run) ? (
        <p>
          <Link to={executionPath(run.runId)}>Abrir o acompanhamento</Link>
        </p>
      ) : null}
      <ResourceView resource={detail} loading="Lendo o detalhe…" fallbackError="Não foi possível ler o detalhe.">
        {(value) => <Body detail={value} />}
      </ResourceView>
    </div>
  );
}

function Body({ detail }: { detail: RunDetail }) {
  const nothing =
    detail.operations.length === 0 &&
    detail.steps.length === 0 &&
    detail.evals.length === 0 &&
    detail.actions.length === 0 &&
    detail.notApplied.length === 0 &&
    detail.pending.length === 0;

  return (
    <>
      {detail.steps.length > 0 ? (
        <DetailBlock title="Passos">
          <ul className={styles.plain}>
            {detail.steps.map((step) => (
              <li key={step.id}>
                <span className={styles.itemTitle}>{step.title}</span>
                <span className={styles.meta}> · {stepStatus(step.status)}</span>
                {step.detail ? <span className={styles.meta}>: {step.detail}</span> : null}
              </li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.operations.length > 0 ? (
        <DetailBlock title="O que mudou">
          <ul className={styles.plain}>
            {detail.operations.map((operation) => (
              <li key={`${operation.op}:${operation.path}`}>
                <span className={styles.mono}>{operation.path}</span>
                <span className={styles.meta}>
                  {' '}
                  · {operationText(operation.op)}, {operationStatus(operation.status)}
                  {operation.note ? `: ${operation.note}` : ''}
                </span>
              </li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.evals.length > 0 ? (
        <DetailBlock title="Verificações">
          <ul className={styles.plain}>
            {detail.evals.map((evaluation) => (
              <li key={evaluation.id}>
                <span className={styles.mono}>{evaluation.id}</span>
                <span className={styles.meta}>
                  {' '}
                  · {evaluation.status}
                  {evaluation.required ? ' (obrigatória)' : ''}
                </span>
              </li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.actions.length > 0 ? (
        <DetailBlock title="Ações propostas por agentes">
          <ul className={styles.plain}>
            {detail.actions.map((action) => (
              <li key={action.id}>
                <span className={styles.itemTitle}>{action.summary}</span>
                <span className={styles.meta}>
                  {' '}
                  · {action.target} · {action.status}
                </span>
              </li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.notApplied.length > 0 ? (
        <DetailBlock title="Ficou de fora">
          <ul className={styles.plain}>
            {detail.notApplied.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.pending.length > 0 ? (
        <DetailBlock title="Pendente">
          <ul className={styles.plain}>
            {detail.pending.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
        </DetailBlock>
      ) : null}
      {detail.model ? (
        <DetailBlock title="Modelo">
          <p className={styles.meta}>
            {detail.model.calls} {detail.model.calls === 1 ? 'chamada' : 'chamadas'} ·{' '}
            {detail.model.inputTokens + detail.model.outputTokens} tokens
          </p>
        </DetailBlock>
      ) : null}
      {nothing ? <p className={styles.meta}>Esta execução não registrou passos nem arquivos.</p> : null}
      <p className={styles.meta}>
        {detail.eventCount === 0
          ? 'Nenhum evento registrado.'
          : `${detail.eventCount} ${detail.eventCount === 1 ? 'evento registrado' : 'eventos registrados'}.`}
      </p>
    </>
  );
}

function DetailBlock({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={styles.block}>
      <h3 className={styles.blockTitle}>{title}</h3>
      {children}
    </section>
  );
}

const STEP_STATUS: Record<string, string> = {
  pending: 'pendente',
  running: 'em andamento',
  completed: 'concluído',
  completed_with_conflicts: 'concluído com conflitos'
};

function stepStatus(status: string): string {
  return STEP_STATUS[status] ?? status;
}

const OPERATION: Record<string, string> = { create: 'criado', update: 'atualizado', delete: 'removido' };

function operationText(op: string): string {
  return OPERATION[op] ?? op;
}

const OPERATION_STATUS: Record<string, string> = {
  pending: 'pendente',
  applied: 'aplicado',
  reverted: 'revertido',
  conflict: 'em conflito'
};

function operationStatus(status: string): string {
  return OPERATION_STATUS[status] ?? status;
}
