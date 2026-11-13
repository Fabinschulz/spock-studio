import { useProjectSelectionContext } from '@/features/project-selection';
import { TimelinePage } from '@/features/timeline';
import { tauriEngineClient } from '@/shared/api';

export default function Timeline() {
  const { selected } = useProjectSelectionContext();
  return <TimelinePage client={tauriEngineClient} project={selected} />;
}
