import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import { useMemo } from 'react';
import { Link } from 'react-router';
import { JourneyFrame } from './components/journey-frame';
import { ProjectChoice } from './components/project-choice';
import { ConfigureJourney } from './configure-journey';
import { createHarnessApi, type HarnessApi } from './service';

interface ConfigureProjectPageProps {
  /** Padrão: o motor real, pelo host Tauri. Os testes injetam um dublê. */
  api?: HarnessApi;
  pollMs?: number;
}

export function ConfigureProjectPage({ api, pollMs }: ConfigureProjectPageProps) {
  const { selected } = useProjectSelectionContext();
  const resolvedApi = useMemo(() => api ?? createHarnessApi(tauriEngineClient), [api]);

  if (!selected) {
    return (
      <JourneyFrame step="project">
        <ProjectChoice />
      </JourneyFrame>
    );
  }

  return (
    <>
      <p>
        <Link to={pathTo('engineeringRequest')}>Pedido de engenharia</Link> — analisar objetivo, evidências e impacto
        sem alterar o Harness.
      </p>
      <ConfigureJourney
        key={selected.projectId}
        api={resolvedApi}
        projectId={selected.projectId}
        projectName={selected.displayName}
        projectPath={selected.displayPath}
        pollMs={pollMs}
      />
    </>
  );
}
