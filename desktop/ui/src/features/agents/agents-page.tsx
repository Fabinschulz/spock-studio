import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import {
  ErrorState,
  InlineNotice,
  LoadingState,
  NoProjectState,
  Page,
  PageSection,
  Radio,
  ResourceView,
  StatusChip,
  Term
} from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { AiTool } from '@/shared/schema';
import { runStateLabel } from '@/shared/utils';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import styles from './agents-page.module.scss';
import { useAgentRun, type AgentRun } from './hooks/use-agent-run';
import { createAgentApi, type AgentApi } from './service/agent-api';

export const MAX_PROMPT = 8000;

const FAILURE: Record<string, string> = {
  AGENT_NOT_INSTALLED: 'A ferramenta não foi encontrada neste computador.',
  AGENT_TIMEOUT: 'A ferramenta passou de 10 minutos sem terminar e foi encerrada.',
  AGENT_FAILED: 'A ferramenta terminou com erro. A saída acima mostra o que ela disse; muitas vezes é falta de login.'
};

export function AgentsPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createAgentApi(client), [client]);
  if (!project) {
    return (
      <Page title="Agentes de IA">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectAgents key={project.projectId} api={api} project={project} />;
}

function ProjectAgents({ api, project }: { api: AgentApi; project: SelectedProject }) {
  const tools = useAsyncResource(useCallback(() => api.tools(), [api]));
  const { run, start, cancel } = useAgentRun(api);
  const [chosen, setChosen] = useState<string | null>(null);
  const [prompt, setPrompt] = useState('');
  const busy = run.status === 'starting' || run.status === 'running';

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (chosen && prompt.trim()) void start(project.projectId, chosen, prompt);
  };

  return (
    <Page
      title="Agentes de IA"
      description={
        <>
          Peça a uma ferramenta de IA instalada neste computador que leia {project.displayName} e responda. Ela trabalha
          só para leitura: não muda arquivos nem roda comandos. Mudanças no projeto passam pelo <Term id="harness" />,
          com plano e aprovação.
        </>
      }
    >
      <form className={styles.form} aria-label="Pedido a um agente" onSubmit={submit}>
        <ResourceView
          resource={tools}
          loading="Procurando as ferramentas…"
          fallbackError="Não foi possível listar as ferramentas."
        >
          {(list) => <ToolChoice tools={list} chosen={chosen} disabled={busy} onChoose={setChosen} />}
        </ResourceView>
        <TextField
          label="O que você quer saber"
          multiline
          minRows={3}
          fullWidth
          value={prompt}
          disabled={busy}
          onChange={(event) => setPrompt(event.target.value)}
          helperText={`Até ${MAX_PROMPT.toLocaleString('pt-BR')} caracteres. O pedido vai para a ferramenta escolhida, que usa a conta dela.`}
          slotProps={{ htmlInput: { maxLength: MAX_PROMPT } }}
        />
        {!chosen || !prompt.trim() ? (
          <p className={styles.hint}>Escolha uma ferramenta instalada e escreva o pedido para enviar.</p>
        ) : null}
        <div className={styles.actions}>
          <Button type="submit" variant="contained" disabled={busy || !chosen || !prompt.trim()}>
            {busy ? 'Pedindo…' : 'Pedir ao agente'}
          </Button>
          {run.status === 'running' ? (
            <Button type="button" variant="outlined" color="inherit" onClick={() => void cancel()}>
              Cancelar
            </Button>
          ) : null}
        </div>
      </form>

      <RunView run={run} />
    </Page>
  );
}

function ToolChoice({
  tools,
  chosen,
  disabled,
  onChoose
}: {
  tools: AiTool[];
  chosen: string | null;
  disabled: boolean;
  onChoose: (id: string) => void;
}) {
  const installed = tools.filter((tool) => tool.installed);
  return (
    <fieldset className={styles.tools} disabled={disabled}>
      <legend>Ferramenta</legend>
      {tools.map((tool) => (
        <Radio
          key={tool.id}
          name="agente"
          value={tool.id}
          checked={chosen === tool.id}
          disabled={!tool.installed}
          onChange={() => onChoose(tool.id)}
          label={
            <span className={styles.toolLabel}>
              {tool.name}
              <span className={styles.toolMeta}>
                {tool.installed
                  ? tool.version
                    ? `versão ${tool.version}`
                    : 'instalada'
                  : 'não instalada neste computador'}
              </span>
            </span>
          }
        />
      ))}
      {installed.length === 0 ? (
        <p className={styles.hint}>
          Nenhuma ferramenta de IA foi encontrada. Instale uma delas (por exemplo, Claude Code ou Codex) e faça o login
          dela; depois, volte a esta tela.
        </p>
      ) : null}
    </fieldset>
  );
}

function RunView({ run }: { run: AgentRun }) {
  if (run.status === 'idle') return null;
  if (run.status === 'starting') return <LoadingState>Iniciando a ferramenta…</LoadingState>;
  if (run.status === 'failed') return <ErrorState>{run.message}</ErrorState>;

  const finished = run.status === 'finished';
  return (
    <PageSection
      title="Resposta"
      action={
        finished ? (
          <StatusChip tone={run.state === 'completed' ? 'success' : 'neutral'}>{runStateLabel(run.state)}</StatusChip>
        ) : undefined
      }
    >
      {finished && run.result?.error ? (
        <InlineNotice tone="danger" title="O agente não concluiu">
          {FAILURE[run.result.error] ?? 'A ferramenta não concluiu o pedido.'}
        </InlineNotice>
      ) : null}
      {finished && run.result?.answer ? <p className={styles.answer}>{run.result.answer}</p> : null}
      <details className={styles.output} open={!finished}>
        <summary>{finished ? 'O que a ferramenta escreveu' : 'A ferramenta está escrevendo…'}</summary>
        <pre className={styles.log} aria-live="off">
          {run.lines.length > 0 ? run.lines.join('\n') : 'Nada ainda.'}
        </pre>
      </details>
      {finished ? (
        <p className={styles.hint}>
          O pedido ficou registrado em <Link to={pathTo('runs')}>Execuções</Link>.
        </p>
      ) : null}
    </PageSection>
  );
}
