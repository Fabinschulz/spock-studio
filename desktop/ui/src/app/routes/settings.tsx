import { ModelSettingsPage } from '@/features/settings';
import { tauriEngineClient } from '@/shared/api';

export default function Settings() {
  return <ModelSettingsPage client={tauriEngineClient} />;
}
