import { ProjectPage } from '@/features/project';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Project() {
  const { selected } = useProjectSelectionContext();
  return <ProjectPage client={tauriEngineClient} project={selected} />;
}
