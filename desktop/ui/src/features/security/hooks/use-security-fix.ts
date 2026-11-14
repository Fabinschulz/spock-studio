import { errorMessage } from '@/shared/api';
import type { SecurityFixResult, SecurityPlan } from '@/shared/schema';
import { useCallback, useState } from 'react';

import type { SecurityApi } from '../service/security-api';

export type SecurityFix =
  | { status: 'idle' }
  | { status: 'planning' }
  | { status: 'reviewing'; plan: SecurityPlan }
  | { status: 'applying'; plan: SecurityPlan }
  | { status: 'applied'; result: SecurityFixResult }
  | { status: 'reverting'; result: SecurityFixResult }
  | { status: 'reverted' }
  | { status: 'failed'; message: string };

const failed = (cause: unknown, fallback: string): SecurityFix => ({
  status: 'failed',
  message: errorMessage(cause, fallback)
});

/** Revisar, aprovar e, se preciso, desfazer. `onChanged` refaz a varredura depois de cada gravação. */
export function useSecurityFix(api: SecurityApi, projectId: string, onChanged: () => void) {
  const [fix, setFix] = useState<SecurityFix>({ status: 'idle' });

  const review = useCallback(async () => {
    setFix({ status: 'planning' });
    try {
      setFix({ status: 'reviewing', plan: await api.plan(projectId) });
    } catch (cause) {
      setFix(failed(cause, 'Não foi possível montar a correção.'));
    }
  }, [api, projectId]);

  const apply = useCallback(
    async (plan: SecurityPlan) => {
      setFix({ status: 'applying', plan });
      try {
        const snapshot = await api.fix(projectId, plan.planId);
        if (!snapshot.result) throw new Error('sem resultado');
        setFix(
          snapshot.result.outcome === 'completed'
            ? { status: 'applied', result: snapshot.result }
            : { status: 'failed', message: snapshot.result.error ?? 'A correção não foi aplicada.' }
        );
      } catch (cause) {
        setFix(failed(cause, 'Não foi possível aplicar a correção.'));
      } finally {
        onChanged();
      }
    },
    [api, projectId, onChanged]
  );

  const revert = useCallback(
    async (result: SecurityFixResult) => {
      setFix({ status: 'reverting', result });
      try {
        await api.revert(projectId, result.runId);
        setFix({ status: 'reverted' });
      } catch (cause) {
        setFix(failed(cause, 'Não foi possível desfazer a correção.'));
      } finally {
        onChanged();
      }
    },
    [api, projectId, onChanged]
  );

  const close = useCallback(() => setFix({ status: 'idle' }), []);

  return { fix, review, apply, revert, close };
}
