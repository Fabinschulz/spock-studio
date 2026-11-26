import { useProjectSelectionContext } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, Page, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';
import { Link, useNavigate } from 'react-router';

import { PortfolioTable } from './components/portfolio-table';
import { resolveOpenAction } from './domain/open-project';
import styles from './management.module.scss';
import { createManagementApi } from './service/management-api';

/** Os pontos de atenção saem de regras fixas do motor, não de previsão. */
export function PortfolioPage({ client }: { client: EngineClient }) {
  const api = useMemo(() => createManagementApi(client), [client]);
  const load = useCallback(() => api.portfolio(), [api]);
  const portfolio = useAsyncResource(load);
  const { selected, recents, busy, openRecent, setSwitcherOpen } = useProjectSelectionContext();
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
            <>
              <EmptyState>
                Nenhum projeto aberto neste computador ainda. Escolha um dos recentes ou abra uma pasta, e ele aparece
                aqui.
              </EmptyState>
              <div>
                <Button type="button" variant="contained" onClick={() => setSwitcherOpen(true)}>
                  Escolher um projeto
                </Button>
              </div>
            </>
          ) : (
            <>
              <PortfolioTable
                projects={value.projects}
                weeks={value.periodWeeks}
                actionOf={(project) => resolveOpenAction(project, selected, recents)}
                busy={busy}
                onOpen={(recentId) => void open(recentId)}
              />
              {value.projects.length > 1 ? (
                <p className={styles.muted}>
                  Para uma pergunta que cruza os projetos, use o <Link to={pathTo('assistant')}>Assistente</Link>, em
                  "Vários projetos".
                </p>
              ) : null}
            </>
          )
        }
      </ResourceView>
    </Page>
  );
}
