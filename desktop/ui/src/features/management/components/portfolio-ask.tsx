import { errorMessage } from '@/shared/api';
import { Disclosure, ErrorState, LoadingState } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { PortfolioAnswer, PortfolioProject } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useCallback, useId, useState, type FormEvent } from 'react';

import { MAX_QUESTION, MIN_QUESTION, questionHint } from '../domain/assistant';
import { useAsk } from '../hooks/use-ask';
import askStyles from './ask.module.scss';
import styles from '../management.module.scss';
import type { ManagementApi } from '../service/management-api';
import { AssistantAnswerView } from './assistant-answer';
import { AskErrorNotice, ModelKeyNotice } from './model-key';

/** A política de cada projeto vale por inteiro: título de item só entra no projeto cujas fontes autorizaram. */
export function PortfolioAsk({ api, projects }: { api: ManagementApi; projects: PortfolioProject[] }) {
  const [chosen, setChosen] = useState<string[]>(() => projects.map((project) => project.projectId));
  const [question, setQuestion] = useState('');
  const { busy, error, run } = useAsk();
  const [answer, setAnswer] = useState<PortfolioAnswer | null>(null);

  const status = useAsyncResource(useCallback(() => api.modelStatus(), [api]));
  const preview = useAsyncResource(
    useCallback(() => (chosen.length === 0 ? Promise.resolve(null) : api.portfolioEvidence(chosen)), [api, chosen])
  );

  const trimmed = question.trim();
  const valid = trimmed.length >= MIN_QUESTION && trimmed.length <= MAX_QUESTION && chosen.length > 0;
  const configured = status.status === 'ready' && status.value.configured;
  const hintId = useId();
  const blocked = !configured
    ? 'Para perguntar ao portfólio, configure uma chave de modelo.'
    : chosen.length === 0
      ? 'Escolha pelo menos um projeto.'
      : questionHint(question);

  const toggle = (projectId: string) =>
    setChosen((current) =>
      current.includes(projectId) ? current.filter((id) => id !== projectId) : [...current, projectId]
    );

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (!valid || busy) return;
    setAnswer(await run(() => api.askPortfolio(trimmed, chosen)));
  };

  return (
    <section aria-labelledby="portfolio-ask" className={askStyles.answerGroup}>
      <h2 id="portfolio-ask" className={styles.section}>
        Perguntar sobre o portfólio
      </h2>
      <p className={styles.muted}>
        Uma pergunta que cruza os projetos escolhidos. Cada projeto entra com os fatos dele e a política dele: título de
        item só vai dos projetos cujas fontes autorizaram servir de contexto.
      </p>

      {status.status === 'ready' && !status.value.configured ? (
        <ModelKeyNotice>O assistente usa um modelo de linguagem para escrever a resposta.</ModelKeyNotice>
      ) : null}
      {status.status === 'error' ? (
        <ErrorState onRetry={status.reload}>
          {errorMessage(status.error, 'Não foi possível verificar a chave do modelo.')}
        </ErrorState>
      ) : null}

      <form aria-label="Pergunta ao portfólio" onSubmit={(event) => void ask(event)}>
        <fieldset className={askStyles.chooser}>
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
          <Button
            type="submit"
            variant="contained"
            disabled={busy || blocked !== null}
            aria-describedby={blocked ? hintId : undefined}
          >
            {busy ? 'Perguntando…' : 'Perguntar ao portfólio'}
          </Button>
          {blocked && !busy ? (
            <p id={hintId} className={styles.hint}>
              {blocked}
            </p>
          ) : null}
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
          <ul className={askStyles.factList}>
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
        <ErrorState onRetry={preview.reload}>
          {errorMessage(preview.error, 'Não foi possível montar o que será enviado.')}
        </ErrorState>
      ) : null}

      {error ? <AskErrorNotice error={error} /> : null}
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
