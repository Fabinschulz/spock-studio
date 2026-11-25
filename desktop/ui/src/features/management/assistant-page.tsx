import type { SelectedProject } from '@/features/project-selection';
import { EngineError, errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice, ErrorState, LoadingState, NoProjectState, Page, Disclosure } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo, readHandoff } from '@/shared/routing';
import type { AssistantAnswer, ProjectInvestigation } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState, type FormEvent } from 'react';
import { Link, useLocation } from 'react-router';

import { AssistantAnswerView } from './components/assistant-answer';
import { InvestigationPath } from './components/investigation-path';
import { MAX_QUESTION, MIN_QUESTION, SUGGESTED_QUESTIONS } from './domain/assistant';
import styles from './management-page.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

/**
 * Os números vêm do motor; o modelo só explica, e a resposta é conferida antes de aparecer. Nada vai para o provedor
 * sem chave configurada e sem o clique em "Perguntar".
 */
export function AssistantPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createManagementApi(client), [client]);

  if (!project) {
    return (
      <Page title="Assistente">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectAssistant key={project.projectId} api={api} project={project} />;
}

function ProjectAssistant({ api, project }: { api: ManagementApi; project: SelectedProject }) {
  const load = useCallback(async () => {
    const [status, evidence] = await Promise.all([api.modelStatus(), api.evidence(project.projectId)]);
    return { configured: status.configured, evidence };
  }, [api, project.projectId]);
  const context = useAsyncResource(load);
  const { state } = useLocation();
  const [question, setQuestion] = useState(() => readHandoff(state)?.slice(0, MAX_QUESTION) ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ message: string; needsKey: boolean } | null>(null);
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [investigation, setInvestigation] = useState<ProjectInvestigation | null>(null);
  const [steps, setSteps] = useState(false);

  const trimmed = question.trim();
  const valid = trimmed.length >= MIN_QUESTION && trimmed.length <= MAX_QUESTION;

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (!valid || busy) return;
    setBusy(true);
    setError(null);
    try {
      if (steps) {
        const result = await api.investigate(project.projectId, trimmed);
        setInvestigation(result);
        setAnswer(result.answer);
      } else {
        setInvestigation(null);
        setAnswer(await api.ask(project.projectId, trimmed));
      }
    } catch (cause) {
      setAnswer(null);
      setInvestigation(null);
      setError({
        message: errorMessage(cause, 'Não foi possível obter a resposta.'),
        needsKey: cause instanceof EngineError && cause.code === 'MODEL_NOT_CONFIGURED'
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Page
      title="Assistente"
      description={`${project.displayName} · pergunte sobre andamento, riscos e previsão. A resposta separa o que foi observado, o que foi calculado, o que é interpretação e o que não está disponível.`}
    >
      {context.status === 'loading' ? <LoadingState>Preparando os fatos do projeto…</LoadingState> : null}
      {context.status === 'error' ? (
        <ErrorState onRetry={context.reload}>
          {errorMessage(context.error, 'Não foi possível ler os fatos do projeto.')}
        </ErrorState>
      ) : null}

      {context.status === 'ready' ? (
        <>
          {!context.value.configured ? (
            <InlineNotice tone="warning" title="Nenhuma chave de modelo configurada">
              O assistente usa um modelo de linguagem para escrever a explicação (sem ele, só a investigação em etapas
              roda, e lista os sinais). Configure a chave em <Link to={pathTo('settings')}>Configurações</Link>.
            </InlineNotice>
          ) : null}

          <form className={styles.askForm} aria-label="Pergunta ao assistente" onSubmit={(event) => void ask(event)}>
            <TextField
              label="Sua pergunta"
              multiline
              minRows={2}
              fullWidth
              value={question}
              disabled={busy}
              onChange={(event) => setQuestion(event.target.value)}
              helperText={`Entre ${MIN_QUESTION} e ${MAX_QUESTION} caracteres.`}
            />
            <div className={styles.actions}>
              <Button
                type="submit"
                variant="contained"
                disabled={busy || !valid || (!steps && !context.value.configured)}
              >
                {busy ? (steps ? 'Investigando…' : 'Perguntando…') : steps ? 'Investigar' : 'Perguntar'}
              </Button>
              <Checkbox
                label="Investigar em etapas"
                checked={steps}
                disabled={busy}
                onChange={(_, checked) => setSteps(checked)}
              />
            </div>
            <div className={styles.suggestions} role="group" aria-label="Sugestões de pergunta">
              {SUGGESTED_QUESTIONS.map((text) => (
                <Button
                  key={text}
                  type="button"
                  variant="outlined"
                  size="small"
                  disabled={busy}
                  onClick={() => setQuestion(text)}
                >
                  {text}
                </Button>
              ))}
            </div>
          </form>

          <Disclosure
            className={styles.method}
            summary={
              <>O que será enviado ao provedor de modelo: a pergunta e {context.value.evidence.facts.length} fatos</>
            }
          >
            <ul>
              {context.value.evidence.withheld.map((text) => (
                <li key={text}>{text}</li>
              ))}
            </ul>
            <ul className={styles.factList}>
              {context.value.evidence.facts.map((fact) => (
                <li key={fact.id}>{fact.text}</li>
              ))}
            </ul>
          </Disclosure>

          {error ? (
            <p className={styles.errorText} role="alert">
              {error.message}
              {error.needsKey ? (
                <>
                  {' '}
                  Configure a chave em <Link to={pathTo('settings')}>Configurações</Link>.
                </>
              ) : null}
            </p>
          ) : null}
          {busy ? (
            <LoadingState>
              {steps ? 'Cruzando riscos, execuções e mudanças…' : 'O modelo está escrevendo a resposta…'}
            </LoadingState>
          ) : null}
          {answer ? <AssistantAnswerView answer={answer} /> : null}
          {investigation ? <InvestigationPath investigation={investigation} /> : null}
        </>
      ) : null}
    </Page>
  );
}
