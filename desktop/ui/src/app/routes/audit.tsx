import { AuditPage } from '@/features/audit';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Audit() {
  const { selected } = useProjectSelectionContext();
  return <AuditPage client={tauriEngineClient} project={selected} />;
}
