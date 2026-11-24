import { formatEngineDate } from '@/shared/utils';
import { useProjectSelectionContext } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { StatusChip, EmptyState, Page, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { PortfolioProject } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';
import { Link, useNavigate } from 'react-router';

import { resolveOpenAction, type OpenAction } from './domain/open-project';
import styles from './management-page.module.scss';
import { PortfolioAsk } from './components/portfolio-ask';
import { createManagementApi } from './service/management-api';

const KIND_LABEL: Record<string, string> = { issues: 'Issues', pullRequests: 'Pull requests' };

/** Os pontos de atenção saem de regras fixas do motor, não de previsão. */
export function PortfolioPage({ client }: { client: EngineClient }) {
  const api = useMemo(() => createManagementApi(client), [client]);
  const load = useCallback(() => api.portfolio(), [api]);
  const portfolio = useAsyncResource(load);
  const { selected, recents, busy, openRecent } = useProjectSelectionContext();
  const navigate = useNavigate();

  async function open(recentId: string) {
    if (await openRecent(recentId)) navigate(pathTo('management'));
  }

  return (
    <Page
      title="Portfólio"
      description="Os projetos abertos neste computador e o que as ferramentas vinculadas a cada um mostram. Quem pede atenção aparece primeiro."
    >
      <ResourceView resource={portfolio} loading="Lendo o portfólio…" fallbackError="Não foi possível ler o portfólio.">
        {(value) =>
          value.projects.length === 0 ? (
            <EmptyState>
              Nenhum projeto aberto ainda. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link>.
            </EmptyState>
          ) : (
            <>
              <ul className={styles.rows} aria-label="Projetos">
                {value.projects.map((project) => (
                  <ProjectRow
                    key={project.projectId}
                    project={project}
                    weeks={value.periodWeeks}
                    action={resolveOpenAction(project, selected, recents)}
                    busy={busy}
                    onOpen={(recentId) => void open(recentId)}
                  />
                ))}
              </ul>
              <PortfolioAsk api={api} projects={value.projects} />
              {value.projects.some((project) => !resolveOpenAction(project, selected, recents)) ? (
                <p className={styles.muted}>
                  Um projeto sem o botão de abrir não está entre os recentes. Escolha a pasta dele na{' '}
                  <Link to={pathTo('home')}>tela inicial</Link>.
                </p>
              ) : null}
            </>
          )
        }
      </ResourceView>
    </Page>
  );
}

interface ProjectRowProps {
  project: PortfolioProject;
  weeks: number;
  action: OpenAction;
  busy: boolean;
  onOpen: (recentId: string) => void;
}

function ProjectRow({ project, weeks, action, busy, onOpen }: ProjectRowProps) {
  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span>
          <span className={styles.rowTitle}>{project.displayName}</span>
          <span className={styles.muted}> · {project.displayPath}</span>
        </span>
        <span className={styles.portfolioEnd}>
          {project.signals.length === 0 ? <StatusChip tone="success">Sem pontos de atenção</StatusChip> : null}
          {action?.kind === 'current' ? (
            <>
              <StatusChip tone="info">Aberto agora</StatusChip>
              <Link to={pathTo('management')} aria-label={`Ver acompanhamento de ${project.displayName}`}>
                Ver acompanhamento
              </Link>
            </>
          ) : null}
          {action?.kind === 'recent' ? (
            <Button
              type="button"
              variant="outlined"
              size="small"
              aria-label={`Abrir projeto ${project.displayName}`}
              aria-disabled={busy || undefined}
              onClick={busy ? undefined : () => onOpen(action.recentId)}
            >
              Abrir projeto
            </Button>
          ) : null}
        </span>
      </div>

      {project.kinds.map((kind) => (
        <p key={kind.kind} className={styles.muted}>
          <span className={styles.kindLabel}>{KIND_LABEL[kind.kind] ?? kind.kind}:</span>{' '}
          {count(kind.open, 'aberto', 'abertos')} · {kind.inProgress} em andamento ·{' '}
          {count(kind.deliveredInPeriod, 'entregue', 'entregues')} em {weeks} semanas
          {kind.overdue !== null ? ` · ${kind.overdue} com prazo vencido` : ''}
        </p>
      ))}
      {project.sources > 0 ? (
        <p className={styles.muted}>
          {project.sources} {project.sources === 1 ? 'fonte' : 'fontes'}
          {project.oldestReadAt ? ` · leitura mais antiga ${formatEngineDate(project.oldestReadAt)}` : ''}
        </p>
      ) : null}

      {project.signals.length > 0 ? (
        <ul className={styles.signals} aria-label={`Pontos de atenção de ${project.displayName}`}>
          {project.signals.map((signal) => (
            <li key={signal.id}>{signal.message}</li>
          ))}
        </ul>
      ) : null}
    </li>
  );
}

function count(value: number, one: string, many: string): string {
  return `${value} ${value === 1 ? one : many}`;
}
