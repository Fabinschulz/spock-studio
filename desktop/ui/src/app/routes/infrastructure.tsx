import { InfrastructurePage } from '@/features/infrastructure';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Infrastructure() {
  const { selected } = useProjectSelectionContext();
  return <InfrastructurePage client={tauriEngineClient} project={selected} />;
}
