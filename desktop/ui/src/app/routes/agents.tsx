import { AgentsPage } from '@/features/agents';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Agents() {
  const { selected } = useProjectSelectionContext();
  return <AgentsPage client={tauriEngineClient} project={selected} />;
}
