import { EngineeringRequestPage } from '@/features/engineering-request';
import { tauriEngineClient } from '@/shared/api';

export default function EngineeringRequest() {
  return <EngineeringRequestPage client={tauriEngineClient} />;
}
