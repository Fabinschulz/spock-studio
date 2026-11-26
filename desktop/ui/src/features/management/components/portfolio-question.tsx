import { errorMessage } from '@/shared/api';
import { Disclosure, ErrorState, LoadingState } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { PortfolioAnswer, PortfolioEvidence, PortfolioProject } from '@/shared/schema';
import { plural } from '@/shared/utils';
import { Button, Checkbox } from '@mariliz/ui-kit';
import { useCallback, useId, useState, type FormEvent } from 'react';

import { questionHint } from '../domain/assistant';
import { useAsk } from '../hooks/use-ask';
import styles from '../management.module.scss';
import type { ManagementApi } from '../service/management-api';
import askStyles from './ask.module.scss';
import { AssistantAnswerView } from './assistant-answer';
import { AskErrorNotice } from './model-key';
import { QuestionField } from './question-field';

interface PortfolioQuestionProps {
  api: ManagementApi;
  projects: PortfolioProject[];
  configured: boolean;
  question: string;
  onQuestion: (question: string) => void;
}

/** A política de cada projeto vale por inteiro: título de item só entra no projeto cujas fontes autorizaram. */
export function PortfolioQuestion({ api, projects, configured, question, onQuestion }: PortfolioQuestionProps) {
  const [chosen, setChosen] = useState<string[]>(() => projects.map((project) => project.projectId));
  const { busy, error, run } = useAsk();
  const [answer, setAnswer] = useState<PortfolioAnswer | null>(null);
  const preview = useAsyncResource(
    useCallback(() => (chosen.length === 0 ? Promise.resolve(null) : api.portfolioEvidence(chosen)), [api, chosen])
  );
  const hintId = useId();
  const blocked = !configured
    ? 'Para perguntar a vários projetos, configure uma chave de modelo.'
    : chosen.length === 0
      ? 'Escolha pelo menos um projeto.'
      : questionHint(question);

  const toggle = (projectId: string) =>
    setChosen((current) =>
      current.includes(projectId) ? current.filter((id) => id !== projectId) : [...current, projectId]
    );

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (blocked !== null || busy) return;
    setAnswer(await run(() => api.askPortfolio(question.trim(), chosen)));
  };

  return (
    <>
      <form className={askStyles.askForm} aria-label="Pergunta a vários projetos" onSubmit={(event) => void ask(event)}>
        <fieldset className={askStyles.choices}>
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
        <QuestionField
          label="Sua pergunta sobre os projetos"
          question={question}
          disabled={busy}
          onChange={onQuestion}
        />
        <p className={styles.muted}>
          Cada projeto entra com os fatos dele e a política dele: título de item só vai dos projetos cujas fontes
          autorizaram servir de contexto.
        </p>
        <div className={styles.actions}>
          <Button
            type="submit"
            variant="contained"
            disabled={busy || blocked !== null}
            aria-describedby={blocked ? hintId : undefined}
          >
            {busy ? 'Perguntando…' : 'Perguntar aos projetos'}
          </Button>
          {blocked && !busy ? (
            <p id={hintId} className={styles.hint}>
              {blocked}
            </p>
          ) : null}
        </div>
      </form>

      {preview.status === 'ready' && preview.value ? <EvidencePreview evidence={preview.value} /> : null}
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
    </>
  );
}

/** Os fatos comuns (a data de hoje) entram uma vez: dizê-lo é o que faz a soma dos projetos bater com o total. */
function EvidencePreview({ evidence }: { evidence: PortfolioEvidence }) {
  const perProject = evidence.projects.reduce((sum, project) => sum + project.facts, 0);
  const shared = evidence.facts.length - perProject;

  return (
    <Disclosure
      className={styles.method}
      summary={`O que será enviado ao provedor de modelo: a pergunta e ${evidence.facts.length} fatos de ${plural(evidence.projects.length, 'projeto', 'projetos')}`}
    >
      <ul>
        {evidence.withheld.map((text) => (
          <li key={text}>{text}</li>
        ))}
      </ul>
      <ul className={askStyles.factList}>
        {evidence.projects.map((project) => (
          <li key={project.alias}>
            <strong>{project.name}</strong>: {plural(project.facts, 'fato', 'fatos')}
          </li>
        ))}
        {shared > 0 ? (
          <li>{plural(shared, 'fato comum a todos os projetos', 'fatos comuns a todos os projetos')}</li>
        ) : null}
      </ul>
    </Disclosure>
  );
}
