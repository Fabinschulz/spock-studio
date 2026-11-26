import type { SelectedProject } from '@/features/project-selection';
import { errorMessage } from '@/shared/api';
import { Disclosure, ErrorState, LoadingState } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { AssistantAnswer, ProjectInvestigation } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useId, useState, type FormEvent } from 'react';

import { questionHint } from '../domain/assistant';
import { useAsk } from '../hooks/use-ask';
import styles from '../management.module.scss';
import type { ManagementApi } from '../service/management-api';
import askStyles from './ask.module.scss';
import { AssistantAnswerView } from './assistant-answer';
import { ChoiceGroup, type Choice } from './choice-group';
import { InvestigationPath } from './investigation-path';
import { AskErrorNotice } from './model-key';
import { QuestionField } from './question-field';

type Mode = 'direct' | 'steps';

function modeChoices(configured: boolean): Choice<Mode>[] {
  return [
    {
      value: 'direct',
      label: 'Resposta direta',
      note: configured
        ? 'O modelo de IA escreve a resposta a partir dos fatos do projeto; ela é conferida antes de aparecer.'
        : 'Precisa de uma chave de modelo, e nenhuma está configurada.',
      disabled: !configured
    },
    {
      value: 'steps',
      label: 'Investigação em etapas',
      note: configured
        ? 'Reúne os fatos por grupos, também execuções e mudanças, e o modelo cruza o que encontrou.'
        : 'Reúne os fatos por grupos e lista os sinais de atenção. Sem chave, nada sai deste computador.'
    }
  ];
}

function previewSummary(mode: Mode, configured: boolean, facts: number): string {
  if (mode === 'direct') return `O que será enviado ao provedor de modelo: a pergunta e ${facts} fatos`;
  return configured
    ? `O que pode ir ao provedor de modelo: a pergunta e os fatos que a investigação reunir (${facts} do projeto, mais execuções e mudanças)`
    : `Fatos que a investigação usa: ${facts} do projeto, mais execuções e mudanças. Nada é enviado`;
}

interface ProjectQuestionProps {
  api: ManagementApi;
  project: SelectedProject;
  configured: boolean;
  question: string;
  onQuestion: (question: string) => void;
}

/**
 * Os números vêm do motor; o modelo só explica, e a resposta é conferida antes de aparecer. Sem chave, a resposta direta
 * não existe: a escolha já vem na investigação, que roda só com as regras.
 */
export function ProjectQuestion({ api, project, configured, question, onQuestion }: ProjectQuestionProps) {
  const evidence = useAsyncResource(useCallback(() => api.evidence(project.projectId), [api, project.projectId]));
  const [mode, setMode] = useState<Mode>(configured ? 'direct' : 'steps');
  const { busy, error, run } = useAsk();
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [investigation, setInvestigation] = useState<ProjectInvestigation | null>(null);
  const hintId = useId();
  const blocked = questionHint(question);

  const ask = async (event: FormEvent) => {
    event.preventDefault();
    if (blocked !== null || busy) return;
    const trimmed = question.trim();
    const result = await run(async () => {
      if (mode === 'direct') return { answer: await api.ask(project.projectId, trimmed), investigation: null };
      const found = await api.investigate(project.projectId, trimmed);
      return { answer: found.answer, investigation: found };
    });
    setAnswer(result?.answer ?? null);
    setInvestigation(result?.investigation ?? null);
  };

  return (
    <>
      <form className={askStyles.askForm} aria-label="Pergunta ao assistente" onSubmit={(event) => void ask(event)}>
        <QuestionField label="Sua pergunta" question={question} disabled={busy} onChange={onQuestion} />
        <ChoiceGroup
          legend="Como responder"
          name="assistant-mode"
          value={mode}
          choices={modeChoices(configured)}
          disabled={busy}
          onChange={setMode}
        />
        <div className={styles.actions}>
          <Button
            type="submit"
            variant="contained"
            disabled={busy || blocked !== null}
            aria-describedby={blocked ? hintId : undefined}
          >
            {busy
              ? mode === 'steps'
                ? 'Investigando…'
                : 'Perguntando…'
              : mode === 'steps'
                ? 'Investigar'
                : 'Perguntar'}
          </Button>
          {blocked && !busy ? (
            <p id={hintId} className={styles.hint}>
              {blocked}
            </p>
          ) : null}
        </div>
      </form>

      {evidence.status === 'ready' ? (
        <Disclosure className={styles.method} summary={previewSummary(mode, configured, evidence.value.facts.length)}>
          <ul>
            {evidence.value.withheld.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
          <ul className={askStyles.factList}>
            {evidence.value.facts.map((fact) => (
              <li key={fact.id}>{fact.text}</li>
            ))}
          </ul>
        </Disclosure>
      ) : null}
      {evidence.status === 'error' ? (
        <ErrorState onRetry={evidence.reload}>
          {errorMessage(evidence.error, 'Não foi possível ler os fatos do projeto.')}
        </ErrorState>
      ) : null}

      {error ? <AskErrorNotice error={error} /> : null}
      {busy ? (
        <LoadingState>
          {mode === 'steps' ? 'Cruzando riscos, execuções e mudanças…' : 'O modelo está escrevendo a resposta…'}
        </LoadingState>
      ) : null}
      {answer ? <AssistantAnswerView answer={answer} /> : null}
      {investigation ? (
        <InvestigationPath
          investigation={investigation}
          known={evidence.status === 'ready' ? evidence.value.facts : []}
        />
      ) : null}
    </>
  );
}
