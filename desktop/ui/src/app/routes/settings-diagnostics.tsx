import { DiagnosticsPage } from '@/features/settings';
import { tauriEngineClient } from '@/shared/api';

export default function SettingsDiagnostics() {
  return <DiagnosticsPage client={tauriEngineClient} />;
}
