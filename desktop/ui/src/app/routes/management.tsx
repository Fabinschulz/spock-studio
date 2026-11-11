import { ManagementPage } from '@/features/management';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Management() {
  const { selected } = useProjectSelectionContext();
  return <ManagementPage client={tauriEngineClient} project={selected} />;
}
