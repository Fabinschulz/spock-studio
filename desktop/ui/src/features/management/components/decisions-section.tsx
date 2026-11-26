import { errorMessage } from '@/shared/api';
import { EmptyState, InlineNotice, PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { DECISION_LIMITS, type DecisionSummary, type ManagementDecision, type RunItem } from '@/shared/schema';
import { formatEngineDate, plural, runKindLabel, runStateLabel } from '@/shared/utils';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useId, useState, type FormEvent } from 'react';

import form from './decisions-section.module.scss';
import styles from '../management.module.scss';
import type { ManagementApi } from '../service/management-api';

/** Cada decisão guarda os fatos do momento, não os de hoje, e não se edita nem se apaga. */
export function DecisionsSection({ management, projectId }: { management: ManagementApi; projectId: string }) {
  const [formOpen, setFormOpen] = useState(false);
  const [notice, setNotice] = useState<{ redacted: boolean } | null>(null);
  const load = useCallback(() => management.decisions(projectId), [management, projectId]);
  const list = useAsyncResource(load);

  return (
    <PageSection
      title="Decisões"
      action={
        <Button variant="outlined" onClick={() => setFormOpen((open) => !open)} aria-expanded={formOpen}>
          {formOpen ? 'Fechar' : 'Registrar uma decisão'}
        </Button>
      }
    >
      {formOpen ? (
        <DecisionForm
          management={management}
          projectId={projectId}
          onRecorded={(redacted) => {
            setNotice({ redacted });
            setFormOpen(false);
            list.reload();
          }}
        />
      ) : null}
      {notice ? (
        <InlineNotice tone={notice.redacted ? 'warning' : 'success'}>
          Decisão registrada com os fatos de hoje.
          {notice.redacted ? ' Um trecho do texto parecia um segredo e foi substituído antes de guardar.' : ''}
        </InlineNotice>
      ) : null}
      <ResourceView resource={list} loading="Lendo as decisões…" fallbackError="Não foi possível ler as decisões.">
        {(decisions) =>
          decisions.length === 0 ? (
            formOpen ? null : (
              <EmptyState>
                Nenhuma decisão registrada neste projeto. Registre uma para guardar o que se decidiu, por quê e o que o
                motor sabia naquele dia.
              </EmptyState>
            )
          ) : (
            <ul className={styles.rows} aria-label="Decisões registradas">
              {decisions.map((decision) => (
                <DecisionRow key={decision.id} decision={decision} management={management} projectId={projectId} />
              ))}
            </ul>
          )
        }
      </ResourceView>
    </PageSection>
  );
}

function DecisionForm({
  management,
  projectId,
  onRecorded
}: {
  management: ManagementApi;
  projectId: string;
  onRecorded: (redacted: boolean) => void;
}) {
  const [title, setTitle] = useState('');
  const [decision, setDecision] = useState('');
  const [rationale, setRationale] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hintId = useId();
  const missing = title.trim() === '' || decision.trim() === '';
  const loadRuns = useCallback(() => management.recentRuns(projectId), [management, projectId]);
  const runs = useAsyncResource(loadRuns);

  const toggle = (runId: string) =>
    setSelected((current) =>
      current.includes(runId)
        ? current.filter((id) => id !== runId)
        : [...current, runId].slice(0, DECISION_LIMITS.runs)
    );

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await management.recordDecision({ projectId, title, decision, rationale, runIds: selected });
      onRecorded(result.redacted);
    } catch (cause) {
      setError(errorMessage(cause, 'Não foi possível registrar a decisão.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className={form.decisionForm} onSubmit={(event) => void submit(event)} aria-label="Nova decisão">
      <p className={styles.muted}>
        Os fatos que o motor tem hoje ficam guardados com a decisão. Depois de registrada, ela não pode ser editada nem
        apagada. Não escreva senhas nem chaves: o que parecer um segredo é substituído.
      </p>
      <TextField
        label="Título"
        fullWidth
        required
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        slotProps={{ htmlInput: { maxLength: DECISION_LIMITS.title } }}
      />
      <TextField
        label="O que foi decidido"
        fullWidth
        required
        multiline
        minRows={3}
        value={decision}
        onChange={(event) => setDecision(event.target.value)}
        slotProps={{ htmlInput: { maxLength: DECISION_LIMITS.decision } }}
      />
      <TextField
        label="Por quê (opcional)"
        fullWidth
        multiline
        minRows={3}
        value={rationale}
        onChange={(event) => setRationale(event.target.value)}
        slotProps={{ htmlInput: { maxLength: DECISION_LIMITS.rationale } }}
      />
      <fieldset className={form.runPicker}>
        <legend>Execuções relacionadas (até {DECISION_LIMITS.runs})</legend>
        <ResourceView resource={runs} loading="Lendo as execuções…" fallbackError="Não foi possível ler as execuções.">
          {(value) =>
            value.length === 0 ? (
              <p className={styles.muted}>Este projeto ainda não tem execuções.</p>
            ) : (
              value.map((run) => (
                <RunOption
                  key={run.runId}
                  run={run}
                  checked={selected.includes(run.runId)}
                  onToggle={() => toggle(run.runId)}
                />
              ))
            )
          }
        </ResourceView>
      </fieldset>
      {error ? <InlineNotice tone="danger">{error}</InlineNotice> : null}
      <div className={styles.actions}>
        <Button
          type="submit"
          variant="contained"
          disabled={busy || missing}
          aria-describedby={missing ? hintId : undefined}
        >
          Registrar decisão
        </Button>
        {missing ? (
          <p id={hintId} className={styles.hint}>
            Preencha o título e o que foi decidido.
          </p>
        ) : null}
      </div>
    </form>
  );
}

function RunOption({ run, checked, onToggle }: { run: RunItem; checked: boolean; onToggle: () => void }) {
  return (
    <div className={form.runOption}>
      <Checkbox
        checked={checked}
        onChange={onToggle}
        label={`${runKindLabel(run.kind)} · ${runStateLabel(run.state)} · ${formatEngineDate(run.startedAt)}`}
      />
    </div>
  );
}

function DecisionRow({
  decision,
  management,
  projectId
}: {
  decision: DecisionSummary;
  management: ManagementApi;
  projectId: string;
}) {
  const [facts, setFacts] = useState<ManagementDecision | null>(null);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const toggle = async () => {
    if (open) {
      setOpen(false);
      return;
    }
    if (facts === null) {
      try {
        setFacts(await management.decision(projectId, decision.id));
      } catch (cause) {
        setError(errorMessage(cause, 'Não foi possível abrir os fatos desta decisão.'));
        return;
      }
    }
    setError(null);
    setOpen(true);
  };

  return (
    <li className={styles.item}>
      <div className={styles.itemHead}>
        <span className={styles.rowTitle}>{decision.title}</span>
        <time dateTime={decision.recordedAt} className={styles.muted}>
          {formatEngineDate(decision.recordedAt)}
        </time>
      </div>
      <p>{decision.decision}</p>
      {decision.rationale ? <p className={styles.muted}>Por quê: {decision.rationale}</p> : null}
      {decision.runs.length > 0 ? (
        <p className={styles.muted}>
          {plural(decision.runs.length, 'execução relacionada', 'execuções relacionadas')}:{' '}
          {decision.runs.map((run) => `${runKindLabel(run.kind)} (${runStateLabel(run.state)})`).join(', ')}.
        </p>
      ) : null}
      <div>
        <Button variant="text" onClick={() => void toggle()} aria-expanded={open}>
          {open ? 'Esconder os fatos do dia' : `Ver os fatos do dia (${decision.factCount})`}
        </Button>
      </div>
      {error ? <InlineNotice tone="danger">{error}</InlineNotice> : null}
      {open && facts ? (
        <>
          <ul className={styles.plainList} aria-label={`Fatos de ${decision.title}`}>
            {facts.facts.map((fact) => (
              <li key={fact.id} className={styles.muted}>
                {fact.text}
              </li>
            ))}
          </ul>
          {facts.withheld.map((line) => (
            <p key={line} className={styles.muted}>
              {line}
            </p>
          ))}
        </>
      ) : null}
    </li>
  );
}
