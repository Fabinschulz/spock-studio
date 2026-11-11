import { IntegrationsPage } from '@/features/integrations';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Integrations() {
  const { selected } = useProjectSelectionContext();
  return <IntegrationsPage client={tauriEngineClient} project={selected} />;
}
