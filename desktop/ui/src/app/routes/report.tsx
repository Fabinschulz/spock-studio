import { ReportPage } from '@/features/management';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Report() {
  const { selected } = useProjectSelectionContext();
  return <ReportPage client={tauriEngineClient} project={selected} />;
}
