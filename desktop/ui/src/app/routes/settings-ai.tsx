import { ModelSettingsPage } from '@/features/settings';
import { tauriEngineClient } from '@/shared/api';

export default function SettingsAi() {
  return <ModelSettingsPage client={tauriEngineClient} />;
}
