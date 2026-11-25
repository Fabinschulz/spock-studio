import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, NoProjectState, Page } from '@/shared/components';
import { pathTo, readHandoff } from '@/shared/routing';
import { IDEA_STAGES, type IdeaStage } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router';

import { SavePanel } from './components/save-panel';
import { ScaffoldSection } from './components/scaffold-section';
import { SessionPanel } from './components/session-panel';
import { STAGE_LABELS, StageCard } from './components/stage-card';
import { nextStage, useIdeaFlow } from './hooks/use-idea-flow';
import styles from './idea-page.module.scss';
import { createIdeaApi, type IdeaApi } from './service/idea-api';

export const MIN_IDEA = 20;
export const MAX_IDEA = 4000;

const BUSY_LABEL = {
  drafting: 'O modelo está escrevendo e o motor confere a etapa…',
  reading: 'Lendo e conferindo o que o agente gravou…',
  planning: 'Montando os documentos…',
  saving: 'Gravando os documentos…',
  reverting: 'Desfazendo a gravação…'
} as const;

export function IdeaPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIdeaApi(client), [client]);
  if (!project) {
    return (
      <Page title="Da ideia ao projeto">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectIdea key={project.projectId} api={api} project={project} />;
}

function ProjectIdea({ api, project }: { api: IdeaApi; project: SelectedProject }) {
  const flow = useIdeaFlow(api, project.projectId);
  const { approved, drafts, busy, error, source, design } = flow.state;
  const bySession = source === 'session';
  const { state } = useLocation();
  const [idea, setIdea] = useState(() => readHandoff(state)?.slice(0, MAX_IDEA) ?? '');
  const next = nextStage(approved);
  const started = Boolean(drafts.specification);
  const ideaReady = idea.trim().length >= MIN_IDEA;
  const generate = (stage: IdeaStage) =>
    void flow.draft(stage, stage === 'specification' ? { idea: idea.trim() } : approved);

  return (
    <Page
      title="Da ideia ao projeto"
      description={`Descreva uma ideia e o Spock monta a especificação, a arquitetura e o backlog de ${project.displayName}, uma etapa por vez, com cada escolha de stack pelos critérios do catálogo da Organização. O modelo do Spock ou o agente da sua sessão escreve, o motor confere cada etapa e você aprova antes de seguir.`}
    >
      <div className={styles.form}>
        <TextField
          label="Sua ideia"
          multiline
          minRows={4}
          fullWidth
          value={idea}
          disabled={approved.specification !== undefined || bySession || busy !== null}
          onChange={(event) => setIdea(event.target.value)}
          helperText={`De ${MIN_IDEA} a ${MAX_IDEA.toLocaleString('pt-BR')} caracteres: o problema, quem usa e o que precisa acontecer.`}
          slotProps={{ htmlInput: { maxLength: MAX_IDEA } }}
        />
        <p className={styles.note}>
          Com chave, cada etapa usa o modelo configurado em <Link to={pathTo('settingsAi')}>Minha IA</Link> e aparece em
          Execuções com os tokens gastos.
        </p>
        <div className={styles.actions}>
          {started ? null : (
            <Button
              type="button"
              variant="contained"
              disabled={!ideaReady || busy !== null}
              onClick={() => generate('specification')}
            >
              Gerar especificação
            </Button>
          )}
          {started ? (
            <Button
              type="button"
              variant="outlined"
              color="inherit"
              disabled={busy !== null}
              onClick={() => {
                flow.restart();
                setIdea('');
              }}
            >
              Começar de novo
            </Button>
          ) : null}
        </div>
      </div>

      {started ? null : (
        <SessionPanel idea={idea} design={design} busy={busy !== null} onRead={() => void flow.readDesign()} />
      )}

      {IDEA_STAGES.map((stage, index) => {
        const draft = drafts[stage];
        if (!draft) return null;
        return (
          <StageCard
            key={stage}
            index={index + 1}
            draft={draft}
            approved={approved[stage] !== undefined}
            canApprove={index === 0 || approved[IDEA_STAGES[index - 1]!] !== undefined}
            bySession={bySession}
            busy={busy !== null}
            onApprove={() => flow.approve(stage)}
            onRetry={() => (bySession ? void flow.readDesign() : generate(stage))}
          />
        );
      })}

      {next && next !== 'specification' && !drafts[next] && busy === null && !bySession ? (
        <div className={styles.actions}>
          <Button type="button" variant="contained" onClick={() => generate(next)}>
            Gerar {STAGE_LABELS[next].toLowerCase()}
          </Button>
        </div>
      ) : null}

      {busy ? <LoadingState>{BUSY_LABEL[busy]}</LoadingState> : null}
      {error ? <ErrorState>{error}</ErrorState> : null}

      {next === null ? (
        <SavePanel
          plan={flow.state.plan}
          saved={flow.state.saved}
          reverted={flow.state.reverted}
          busy={busy !== null}
          onReview={() => void flow.review()}
          onSave={(plan) => void flow.save(plan)}
          onCancel={flow.cancelReview}
          onRevert={(saved) => void flow.revert(saved)}
        />
      ) : null}

      <ScaffoldSection api={api} projectId={project.projectId} />
    </Page>
  );
}
