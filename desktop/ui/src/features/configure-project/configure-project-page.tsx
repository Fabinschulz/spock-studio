import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';
import { useMemo } from 'react';
import { useLocation } from 'react-router';
import { JourneyFrame } from './components/journey-frame';
import { ProjectChoice } from './components/project-choice';
import { ConfigureJourney } from './configure-journey';
import { readComponentRequest } from './domain/component-request';
import { createHarnessApi, type HarnessApi } from './service';

interface ConfigureProjectPageProps {
  api?: HarnessApi;
  pollMs?: number;
}

export function ConfigureProjectPage({ api, pollMs }: ConfigureProjectPageProps) {
  const { selected } = useProjectSelectionContext();
  const resolvedApi = useMemo(() => api ?? createHarnessApi(tauriEngineClient), [api]);
  const location = useLocation();
  const componentRequest = useMemo(() => readComponentRequest(location.state), [location.state]);

  if (!selected) {
    return (
      <JourneyFrame step="project">
        <ProjectChoice />
      </JourneyFrame>
    );
  }

  return (
    <ConfigureJourney
      key={selected.projectId}
      api={resolvedApi}
      projectId={selected.projectId}
      projectName={selected.displayName}
      projectPath={selected.displayPath}
      pollMs={pollMs}
      componentRequest={componentRequest}
    />
  );
}
