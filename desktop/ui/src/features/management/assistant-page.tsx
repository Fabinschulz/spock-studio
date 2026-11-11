import type { SelectedProject } from '@/features/project-selection';
import { EngineError, errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { AssistantAnswer } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { AssistantAnswerView } from './components/assistant-answer';
import { MAX_QUESTION, MIN_QUESTION, SUGGESTED_QUESTIONS } from './domain/assistant';
import styles from './management-page.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

/**
 * Perguntas sobre o projeto em linguagem natural. Os números vêm do motor; o modelo só explica, e a resposta é
 * conferida antes de aparecer. Nada vai para o provedor sem chave configurada e sem o clique em "Perguntar".
 */
export function AssistantPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createManagementApi(client), [client]);

  if (!project) {
    return (
      <div className={styles.page}>
        <h1 className={styles.title}>Assistente</h1>
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link>.
        </p>
      </div>
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
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ message: string; needsKey: boolean } | null>(null);
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);

  const trimmed = question.trim();
  const valid = trimmed.length >= MIN_QUESTION && trimmed.length <= MAX_QUESTION;

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (!valid || busy) return;
    setBusy(true);
    setError(null);
    try {
      setAnswer(await api.ask(project.projectId, trimmed));
    } catch (cause) {
      setAnswer(null);
      setError({
        message: errorMessage(cause, 'Não foi possível obter a resposta.'),
        needsKey: cause instanceof EngineError && cause.code === 'MODEL_NOT_CONFIGURED'
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={styles.page}>
      <header>
        <h1 className={styles.title}>Assistente</h1>
        <p className={styles.lead}>
          {project.displayName} · pergunte sobre andamento, riscos e previsão. A resposta separa o que foi observado, o
          que foi calculado, o que é interpretação e o que não está disponível.
        </p>
      </header>

      {context.status === 'loading' ? <p role="status">Preparando os fatos do projeto…</p> : null}
      {context.status === 'error' ? (
        <p role="alert">{errorMessage(context.error, 'Não foi possível ler os fatos do projeto.')}</p>
      ) : null}

      {context.status === 'ready' ? (
        <>
          {!context.value.configured ? (
            <InlineNotice tone="warning" title="Nenhuma chave de modelo configurada">
              O assistente usa um modelo de linguagem para escrever a explicação. Configure a chave em{' '}
              <Link to={pathTo('settings')}>Configurações</Link>.
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
              <Button type="submit" variant="contained" disabled={busy || !valid || !context.value.configured}>
                {busy ? 'Perguntando…' : 'Perguntar'}
              </Button>
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

          <details className={styles.method}>
            <summary>
              O que será enviado ao provedor de modelo: a pergunta e {context.value.evidence.facts.length} fatos
            </summary>
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
          </details>

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
          {busy ? <p role="status">O modelo está escrevendo a resposta…</p> : null}
          {answer ? <AssistantAnswerView answer={answer} /> : null}
        </>
      ) : null}
    </div>
  );
}
