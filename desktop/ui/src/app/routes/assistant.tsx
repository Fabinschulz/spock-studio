import { AssistantPage } from '@/features/management';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Assistant() {
  const { selected } = useProjectSelectionContext();
  return <AssistantPage client={tauriEngineClient} project={selected} />;
}
