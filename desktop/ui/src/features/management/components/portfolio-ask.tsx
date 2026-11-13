import { EngineError, errorMessage } from '@/shared/api';
import { Disclosure, ErrorState, InlineNotice, LoadingState } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { PortfolioAnswer, PortfolioProject } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { MAX_QUESTION, MIN_QUESTION } from '../domain/assistant';
import styles from '../management-page.module.scss';
import type { ManagementApi } from '../service/management-api';
import { AssistantAnswerView } from './assistant-answer';

/** A política de cada projeto vale por inteiro: título de item só entra no projeto cujas fontes autorizaram. */
export function PortfolioAsk({ api, projects }: { api: ManagementApi; projects: PortfolioProject[] }) {
  const [chosen, setChosen] = useState<string[]>(() => projects.map((project) => project.projectId));
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ message: string; needsKey: boolean } | null>(null);
  const [answer, setAnswer] = useState<PortfolioAnswer | null>(null);

  const status = useAsyncResource(useCallback(() => api.modelStatus(), [api]));
  const preview = useAsyncResource(
    useCallback(() => (chosen.length === 0 ? Promise.resolve(null) : api.portfolioEvidence(chosen)), [api, chosen])
  );

  const trimmed = question.trim();
  const valid = trimmed.length >= MIN_QUESTION && trimmed.length <= MAX_QUESTION && chosen.length > 0;
  const configured = status.status === 'ready' && status.value.configured;

  const toggle = (projectId: string) =>
    setChosen((current) =>
      current.includes(projectId) ? current.filter((id) => id !== projectId) : [...current, projectId]
    );

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (!valid || busy) return;
    setBusy(true);
    setError(null);
    try {
      setAnswer(await api.askPortfolio(trimmed, chosen));
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
    <section aria-labelledby="portfolio-ask" className={styles.answerGroup}>
      <h2 id="portfolio-ask" className={styles.section}>
        Perguntar sobre o portfólio
      </h2>
      <p className={styles.muted}>
        Uma pergunta que cruza os projetos escolhidos. Cada projeto entra com os fatos dele e a política dele: título de
        item só vai dos projetos cujas fontes autorizaram servir de contexto.
      </p>

      {status.status === 'ready' && !status.value.configured ? (
        <InlineNotice tone="warning" title="Nenhuma chave de modelo configurada">
          O assistente usa um modelo de linguagem para escrever a resposta. Configure a chave em{' '}
          <Link to={pathTo('settings')}>Configurações</Link>.
        </InlineNotice>
      ) : null}

      <form aria-label="Pergunta ao portfólio" onSubmit={(event) => void ask(event)}>
        <fieldset className={styles.chooser}>
          <legend>Projetos que entram na pergunta</legend>
          {projects.map((project) => (
            <Checkbox
              key={project.projectId}
              label={project.displayName}
              checked={chosen.includes(project.projectId)}
              disabled={busy}
              onChange={() => toggle(project.projectId)}
            />
          ))}
        </fieldset>
        <TextField
          label="Sua pergunta sobre o portfólio"
          multiline
          minRows={2}
          fullWidth
          value={question}
          disabled={busy}
          onChange={(event) => setQuestion(event.target.value)}
          helperText={`Entre ${MIN_QUESTION} e ${MAX_QUESTION} caracteres.`}
        />
        <div className={styles.actions}>
          <Button type="submit" variant="contained" disabled={busy || !valid || !configured}>
            {busy ? 'Perguntando…' : 'Perguntar ao portfólio'}
          </Button>
        </div>
      </form>

      {preview.status === 'ready' && preview.value ? (
        <Disclosure
          className={styles.method}
          summary={
            <>
              O que será enviado ao provedor de modelo: a pergunta e {preview.value.facts.length} fatos de{' '}
              {preview.value.projects.length === 1 ? '1 projeto' : `${preview.value.projects.length} projetos`}
            </>
          }
        >
          <ul>
            {preview.value.withheld.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
          <ul className={styles.factList}>
            {preview.value.projects.map((project) => (
              <li key={project.alias}>
                <strong>
                  {project.alias}: {project.name}
                </strong>{' '}
                ({project.facts} fatos)
              </li>
            ))}
          </ul>
        </Disclosure>
      ) : null}
      {preview.status === 'error' ? (
        <ErrorState>{errorMessage(preview.error, 'Não foi possível montar o pacote.')}</ErrorState>
      ) : null}

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
      {busy ? <LoadingState>O modelo está escrevendo a resposta…</LoadingState> : null}
      {answer ? (
        <>
          <p className={styles.muted}>
            Projetos nesta resposta: {answer.projects.map((project) => `${project.alias} = ${project.name}`).join(', ')}
            .
          </p>
          <AssistantAnswerView answer={answer.answer} />
        </>
      ) : null}
    </section>
  );
}
