import { findScreens } from '@/app/layouts/app-shell';
import { HomePage } from '@/features/home';
import { tauriEngineClient } from '@/shared/api';

export default function Home() {
  return <HomePage client={tauriEngineClient} findShortcuts={findScreens} />;
}
