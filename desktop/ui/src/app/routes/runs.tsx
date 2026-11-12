import { useProjectSelectionContext } from '@/features/project-selection';
import { RunsPage } from '@/features/runs';
import { tauriEngineClient } from '@/shared/api';

export default function Runs() {
  const { selected } = useProjectSelectionContext();
  return <RunsPage client={tauriEngineClient} project={selected} />;
}
