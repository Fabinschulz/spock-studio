import { CloudPage } from '@/features/cloud';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Cloud() {
  const { selected } = useProjectSelectionContext();
  return <CloudPage client={tauriEngineClient} project={selected} />;
}
