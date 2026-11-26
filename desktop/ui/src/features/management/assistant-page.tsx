import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, NoProjectState, Page, Disclosure, Term } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { readHandoff } from '@/shared/routing';
import type { AssistantAnswer, ProjectInvestigation } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useId, useMemo, useState, type FormEvent } from 'react';
import { useLocation } from 'react-router';

import { AssistantAnswerView } from './components/assistant-answer';
import { InvestigationPath } from './components/investigation-path';
import { AskErrorNotice, ModelKeyNotice } from './components/model-key';
import { MAX_QUESTION, MIN_QUESTION, SUGGESTED_QUESTIONS, questionHint } from './domain/assistant';
import askStyles from './components/ask.module.scss';
import { useAsk } from './hooks/use-ask';
import styles from './management.module.scss';
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
  const { busy, error, run } = useAsk();
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [investigation, setInvestigation] = useState<ProjectInvestigation | null>(null);
  const [steps, setSteps] = useState(false);

  const trimmed = question.trim();
  const valid = trimmed.length >= MIN_QUESTION && trimmed.length <= MAX_QUESTION;
  const hintId = useId();
  const configured = context.status === 'ready' && context.value.configured;
  // Sem chave, o motivo vem antes do tamanho da pergunta: escrever não resolve.
  const blocked =
    !steps && !configured ? 'Sem chave de modelo, só a investigação em etapas responde.' : questionHint(question);

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (!valid || busy) return;
    const result = await run(async () => {
      if (!steps) return { answer: await api.ask(project.projectId, trimmed), investigation: null };
      const found = await api.investigate(project.projectId, trimmed);
      return { answer: found.answer, investigation: found };
    });
    setAnswer(result?.answer ?? null);
    setInvestigation(result?.investigation ?? null);
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
            <ModelKeyNotice>
              O assistente usa um modelo de linguagem para escrever a explicação (sem ele, só a{' '}
              <Term id="stepInvestigation" /> roda, e lista os sinais).
            </ModelKeyNotice>
          ) : null}

          <form className={askStyles.askForm} aria-label="Pergunta ao assistente" onSubmit={(event) => void ask(event)}>
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
                disabled={busy || blocked !== null}
                aria-describedby={blocked ? hintId : undefined}
              >
                {busy ? (steps ? 'Investigando…' : 'Perguntando…') : steps ? 'Investigar' : 'Perguntar'}
              </Button>
              <Checkbox
                label="Investigar em etapas"
                checked={steps}
                disabled={busy}
                onChange={(_, checked) => setSteps(checked)}
              />
              {blocked && !busy ? (
                <p id={hintId} className={styles.hint}>
                  {blocked}
                </p>
              ) : null}
            </div>
            <div className={askStyles.suggestions} role="group" aria-label="Sugestões de pergunta">
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
            <ul className={askStyles.factList}>
              {context.value.evidence.facts.map((fact) => (
                <li key={fact.id}>{fact.text}</li>
              ))}
            </ul>
          </Disclosure>

          {error ? <AskErrorNotice error={error} /> : null}
          {busy ? (
            <LoadingState>
              {steps ? 'Cruzando riscos, execuções e mudanças…' : 'O modelo está escrevendo a resposta…'}
            </LoadingState>
          ) : null}
          {answer ? <AssistantAnswerView answer={answer} /> : null}
          {investigation ? (
            <InvestigationPath investigation={investigation} known={context.value.evidence.facts} />
          ) : null}
        </>
      ) : null}
    </Page>
  );
}
