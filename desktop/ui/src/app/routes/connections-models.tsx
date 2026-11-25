import { ModelSettingsPage } from '@/features/settings';
import { tauriEngineClient } from '@/shared/api';

export default function ConnectionsModels() {
  return <ModelSettingsPage client={tauriEngineClient} title="Modelos de IA" />;
}
