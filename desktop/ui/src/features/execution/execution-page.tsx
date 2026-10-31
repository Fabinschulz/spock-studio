import type { EngineClient } from '@/shared/api';
import { Button } from '@synthra.io/ui-kit';
import { useEffect, useState } from 'react';
import { z } from 'zod';

import styles from './execution-page.module.scss';

const evidenceSchema = z.object({
  filePath: z.string(),
  excerpt: z.string(),
  chunkId: z.string()
});

const impactSchema = z.object({
  schema: z.literal('engineering.impact.v1'),
  areas: z.array(z.string()),
  risks: z.array(z.string()),
  evidence: z.array(z.string())
});

const resultSchema = z.object({
  kind: z.literal('workflow'),
  summary: z.string(),
  evidence: z.array(evidenceSchema).default([]),
  limitations: z.array(z.object({ code: z.string(), message: z.string() })).default([]),
  impact: impactSchema.optional()
});

export const workflowSnapshotSchema = z.object({
  runId: z.string(),
  projectId: z.string(),
  kind: z.string(),
  state: z.string(),
  events: z.array(z.object({ seq: z.number(), type: z.string(), data: z.record(z.string(), z.unknown()) })).default([]),
  result: resultSchema.nullable()
});

export type WorkflowSnapshot = z.infer<typeof workflowSnapshotSchema>;

const STATE_LABEL: Record<string, string> = {
  awaiting_approval: 'Aguardando aprovação',
  running: 'Em execução',
  completed_with_limitations: 'Concluído com limitações',
  completed: 'Concluído',
  failed: 'Falhou',
  cancelled: 'Cancelado'
};

const AREA_LABEL: Record<string, string> = {
  servico: 'Serviço',
  interface: 'Interface',
  projeto: 'Projeto'
};

export function ExecutionPage({ runId, client }: { runId: string; client: EngineClient }) {
  const [snapshot, setSnapshot] = useState<WorkflowSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    setSnapshot(null);
    setError(null);
    client
      .request('run.snapshot', { runId }, workflowSnapshotSchema)
      .then((next) => {
        if (active) setSnapshot(next);
      })
      .catch(() => {
        if (active) setError('Não foi possível ler esta execução.');
      });
    return () => {
      active = false;
    };
  }, [client, runId]);

  const approve = async () => {
    if (!snapshot) return;
    setBusy(true);
    setError(null);
    try {
      await client.request('workflow.resume', { projectId: snapshot.projectId, runId }, z.object({ runId: z.string() }));
      const next = await client.request('run.snapshot', { runId }, workflowSnapshotSchema);
      setSnapshot(next);
    } catch {
      setError('Não foi possível retomar a execução.');
    } finally {
      setBusy(false);
    }
  };

  if (error && !snapshot) return <p role="alert">{error}</p>;
  if (!snapshot) return <p role="status">Lendo a execução…</p>;

  const stateLabel = STATE_LABEL[snapshot.state] ?? snapshot.state;
  const agents = snapshot.events.filter((event) => event.type === 'agent.completed');

  return (
    <section className={styles.page} aria-labelledby="execucao-titulo">
      <h1 id="execucao-titulo">Execução</h1>
      <p>
        Estado: <strong>{stateLabel}</strong>
      </p>
      {snapshot.result?.summary ? <p>{snapshot.result.summary}</p> : null}
      {error ? <p role="alert">{error}</p> : null}

      {agents.length > 0 ? (
        <section aria-labelledby="agentes-titulo">
          <h2 id="agentes-titulo">Quem atuou</h2>
          <ul className={styles.list}>
            {agents.map((event) => {
              const id = typeof event.data.agentId === 'string' ? event.data.agentId : '';
              const label = id === 'architecture' ? 'Arquitetura' : id;
              return (
                <li key={event.seq} className={styles.row}>
                  {label || 'Agente'} concluiu
                </li>
              );
            })}
          </ul>
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
