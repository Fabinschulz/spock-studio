import { useProjectSelectionContext } from '@/features/project-selection';
import { SecurityPage } from '@/features/security';
import { tauriEngineClient } from '@/shared/api';

export default function Security() {
  const { selected } = useProjectSelectionContext();
  return <SecurityPage client={tauriEngineClient} project={selected} />;
}
