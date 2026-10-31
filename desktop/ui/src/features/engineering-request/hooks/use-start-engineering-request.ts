import type { EngineClient } from '@/shared/api';
import { EngineError } from '@/shared/api';
import { executionPath } from '@/shared/routing';
import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router';

import type { EngineeringRequestFormValues } from '../domain/form-schema';
import { toWorkflowStartInput } from '../domain/form-schema';
import { startWorkflow } from '../service/workflow-api';

export function useStartEngineeringRequest(client: EngineClient, projectId: string) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = useCallback(
    async (values: EngineeringRequestFormValues) => {
      setBusy(true);
      setError(null);
      try {
        const { runId } = await startWorkflow(client, {
          projectId,
          input: toWorkflowStartInput(values)
        });
        navigate(executionPath(runId));
      } catch (cause) {
        setError(cause instanceof EngineError ? cause.message : 'Não foi possível iniciar o pedido.');
        setBusy(false);
      }
    },
    [client, navigate, projectId]
  );

  return { submit, busy, error, clearError: () => setError(null) };
}
