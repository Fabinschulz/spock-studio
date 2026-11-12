import { HarnessPage } from '@/features/project';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function ProjectHarness() {
  const { selected } = useProjectSelectionContext();
  return <HarnessPage client={tauriEngineClient} project={selected} />;
}
