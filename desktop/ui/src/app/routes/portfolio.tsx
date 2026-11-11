import { PortfolioPage } from '@/features/management';
import { tauriEngineClient } from '@/shared/api';

export default function Portfolio() {
  return <PortfolioPage client={tauriEngineClient} />;
}
