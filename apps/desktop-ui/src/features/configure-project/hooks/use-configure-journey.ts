import { useCallback, useEffect, useReducer, useRef } from 'react';

import { EngineError, waitForRun, type HarnessApi } from '@/shared/services';

import {
  buildApplyInput,
  initialJourneyState,
  journeyReducer,
  type JourneyStepId,
  type RunKind
} from '../core/journey-state';

const messageOf = (error: unknown) =>
  error instanceof EngineError ? error.message : 'Algo inesperado aconteceu. Tente de novo.';

/** Liga o estado da jornada ao motor. Cada ação chama um caso de uso; a decisão é sempre do motor. */
export function useConfigureJourney(api: HarnessApi, projectId: string, pollMs = 1000) {
  const [state, dispatch] = useReducer(journeyReducer, initialJourneyState);
  const abort = useRef<AbortController | null>(null);
  const latest = useRef(state);
  useEffect(() => {
    latest.current = state;
  }, [state]);

  const discover = useCallback(async () => {
    dispatch({ type: 'discover.start' });
    try {
      dispatch({ type: 'discover.ok', discovery: await api.discover(projectId) });
    } catch (error) {
      dispatch({ type: 'discover.error', message: messageOf(error) });
    }
  }, [api, projectId]);

  useEffect(() => {
    void discover();
    return () => abort.current?.abort();
  }, [discover]);

  const generatePlan = useCallback(async () => {
    const { aiEnvironment, allowTlcReinstall, updateTlc } = latest.current;
    dispatch({ type: 'plan.start' });
    try {
      const plan = await api.plan({
        projectId,
        ...(aiEnvironment ? { aiEnvironment } : {}),
        allowTlcForceReinstall: allowTlcReinstall,
        updateTlc
      });
      dispatch({ type: 'plan.ok', plan });
    } catch (error) {
      dispatch({ type: 'plan.error', message: messageOf(error) });
    }
  }, [api, projectId]);

  const track = useCallback(
    async (kind: RunKind, start: () => Promise<{ runId: string }>) => {
      abort.current = new AbortController();
      dispatch({ type: 'run.start', kind });
      try {
        const { runId } = await start();
        dispatch({ type: 'run.id', runId });
        const result = await waitForRun(api, runId, (event) => dispatch({ type: 'run.event', event }), {
          pollMs,
          signal: abort.current.signal
        });
        dispatch({ type: 'run.done', result });
      } catch (error) {
        if (!(error instanceof EngineError && error.code === 'ABORTED'))
          dispatch({ type: 'run.error', message: messageOf(error) });
      }
    },
    [api, pollMs]
  );

  const apply = useCallback(async () => {
    const { plan } = latest.current;
    if (plan.status !== 'ready') return;
    await track('apply', () => api.apply(buildApplyInput(latest.current, projectId, plan.data)));
  }, [api, projectId, track]);

  const revert = useCallback(
    async (runId: string) => track('revert', () => api.revert(projectId, runId)),
    [api, projectId, track]
  );

  const validate = useCallback(async () => track('validate', () => api.validate(projectId)), [api, projectId, track]);

  const cancelRun = useCallback(async () => {
    const runId = latest.current.run?.runId;
    if (runId) await api.cancel(runId);
  }, [api]);

  return {
    state,
    actions: {
      discover,
      generatePlan,
      apply,
      revert,
      validate,
      cancelRun,
      goTo: (step: JourneyStepId) => dispatch({ type: 'step.go', step }),
      setEnvironment: (id: string) => dispatch({ type: 'environment.set', id }),
      setReinstall: (value: boolean) => dispatch({ type: 'reinstall.set', value }),
      setUpdateTlc: (value: boolean) => dispatch({ type: 'update.set', value }),
      toggleChange: (id: string) => dispatch({ type: 'change.toggle', id }),
      toggleCommand: (id: string) => dispatch({ type: 'command.toggle', id }),
      setResolution: (id: string, value: 'keep' | 'overwrite') => dispatch({ type: 'resolution.set', id, value })
    }
  };
}

export type ConfigureJourney = ReturnType<typeof useConfigureJourney>;
