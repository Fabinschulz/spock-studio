import { useProjectSelectionContext } from '@/features/project-selection';
import { QualityPage } from '@/features/quality';
import { tauriEngineClient } from '@/shared/api';

export default function Quality() {
  const { selected } = useProjectSelectionContext();
  return <QualityPage client={tauriEngineClient} project={selected} />;
}
