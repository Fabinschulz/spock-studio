import { errorMessage, type EngineClient } from '@/shared/api';
import { useState } from 'react';

import { pendingApproval, type RunView } from '../domain/run-timeline';
import { resumeWorkflow } from '../service/execution-api';

/**
 * A decisão de quem aprova: quais ações marcar e retomar a execução. Nada vem marcado, porque executar uma ação é uma
 * escolha explícita. Só entram na decisão as ações que o motor realmente propôs.
 */
export function useApproval(
  client: EngineClient,
  runId: string,
  view: RunView | null,
  refresh: () => Promise<void>,
  setError: (message: string | null) => void
) {
  const [busy, setBusy] = useState(false);
  const [chosen, setChosen] = useState<readonly string[]>([]);

  const toggle = (id: string) =>
    setChosen((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));

  const approve = async () => {
    if (!view) return;
    setBusy(true);
    setError(null);
    try {
      const pending = pendingApproval(view.events);
      await resumeWorkflow(
        client,
        view.snapshot.projectId,
        runId,
        pending
          ? {
              approvalId: pending.approvalId,
              actionIds: chosen.filter((id) => pending.actions.some((action) => action.id === id))
            }
          : undefined
      );
      await refresh();
    } catch (cause) {
      setError(errorMessage(cause, 'Não foi possível retomar a execução.'));
    } finally {
      setBusy(false);
    }
  };

  return { busy, chosen, toggle, approve };
}
