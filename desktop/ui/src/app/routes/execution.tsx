import { ExecutionPage } from '@/features/execution';
import { tauriEngineClient } from '@/shared/api';
import { useParams } from 'react-router';

export default function Execution() {
  const { runId = '' } = useParams();
  return <ExecutionPage key={runId} runId={runId} client={tauriEngineClient} />;
}
