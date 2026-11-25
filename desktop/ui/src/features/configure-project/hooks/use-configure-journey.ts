import { EngineError, errorMessage } from '@/shared/api';
import type { ConflictResolution } from '@/shared/schema';
import { useCallback, useEffect, useLayoutEffect, useReducer, useRef } from 'react';
import { initialJourneyState } from '../constants/initial-journey-state';
import type { JourneyStore } from '../context/journey-store';
import { EMPTY_COMPONENT_REQUEST, hasComponentRequest, type ComponentRequest } from '../domain/component-request';
import { buildApplyInput } from '../domain/approvals';
import { journeyReducer } from '../domain/journey-reducer';
import { restorableJourney } from '../domain/restore-journey';
import { plannedApplySteps } from '../domain/planned-steps';
import { waitForRun, type HarnessApi } from '../service';
import { type JourneyState, type JourneyStepId } from '../types/journey';
import { type RunKind } from '../types/run';

const messageOf = (error: unknown) => errorMessage(error, 'Algo inesperado aconteceu. Tente de novo.');

/**
 * Onde a jornada começa. Um pedido novo da tela Projeto é uma ação explícita da pessoa: começa limpo, sem misturar com o
 * que ficou guardado. Sem pedido, retoma o que a pessoa deixou ao sair, se ainda der para retomar.
 */
function startingState(request: ComponentRequest, projectId: string, store: JourneyStore | null): JourneyState {
  const fresh: JourneyState = { ...initialJourneyState, componentRequest: request };
  if (hasComponentRequest(request)) return fresh;
  const saved = store?.read(projectId);
  return (saved && restorableJourney(saved)) ?? fresh;
}

/** A decisão é sempre do motor; com `store`, a jornada sobrevive a sair da tela e voltar. */
export function useConfigureJourney(
  api: HarnessApi,
  projectId: string,
  pollMs = 1000,
  componentRequest: ComponentRequest = EMPTY_COMPONENT_REQUEST,
  store: JourneyStore | null = null
) {
  const [state, dispatch] = useReducer(journeyReducer, componentRequest, (request) =>
    startingState(request, projectId, store)
  );
  const abort = useRef<AbortController | null>(null);
  const latest = useRef(state);
  // Antes de qualquer efeito ou clique: as ações leem o estado mais recente, nunca o de um render anterior.
  useLayoutEffect(() => {
    latest.current = state;
  }, [state]);

  // Cada mudança fica guardada; o que não dá para retomar é filtrado na volta (`restorableJourney`), não aqui.
  useEffect(() => {
    store?.write(projectId, state);
  }, [store, projectId, state]);

  const discover = useCallback(async () => {
    dispatch({ type: 'discover.start' });
    try {
      dispatch({ type: 'discover.ok', discovery: await api.discover(projectId) });
    } catch (error) {
      dispatch({ type: 'discover.error', message: messageOf(error) });
    }
  }, [api, projectId]);

  // A análise só começa quando a pessoa pede ("Analisar projeto"); sair da tela cancela o acompanhamento de execuções.
  useEffect(() => () => abort.current?.abort(), []);

  const generatePlan = useCallback(async () => {
    const {
      aiEnvironments,
      allowSpecificationReinstall,
      updateSpecification,
      componentRequest: request
    } = latest.current;
    dispatch({ type: 'plan.start' });
    try {
      const plan = await api.plan({
        projectId,
        ...(aiEnvironments.length > 0 ? { aiEnvironments } : {}),
        allowSpecificationReinstall: allowSpecificationReinstall,
        updateSpecification,
        ...(request.exclude.length > 0 ? { excludeComponents: request.exclude.map((item) => item.id) } : {}),
        ...(request.include.length > 0 ? { includeComponents: request.include.map((item) => item.id) } : {})
      });
      dispatch({ type: 'plan.ok', plan });
    } catch (error) {
      dispatch({ type: 'plan.error', message: messageOf(error) });
    }
  }, [api, projectId]);

  const track = useCallback(
    async (kind: RunKind, start: () => Promise<{ runId: string }>) => {
      // Uma execução nova substitui a anterior: o acompanhamento antigo para, senão ficaria órfão até o fim.
      abort.current?.abort();
      const controller = new AbortController();
      abort.current = controller;
      const { plan, approvedCommands } = latest.current;
      const planned = kind === 'apply' && plan.status === 'ready' ? plannedApplySteps(plan.data, approvedCommands) : [];
      dispatch({ type: 'run.start', kind, at: Date.now(), planned });
      try {
        const result = await waitForRun(
          api,
          async () => (await start()).runId,
          {
            started: (runId) => dispatch({ type: 'run.id', runId }),
            event: (event) => dispatch({ type: 'run.event', event, at: Date.now() })
          },
          { pollMs, signal: controller.signal }
        );
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

  // A saída do Resultado quando o instalador ficou sem aprovação: um plano novo, já com ele marcado para a revisão.
  const installSpecificationAndReplan = useCallback(async () => {
    dispatch({ type: 'install.set', value: true });
    await generatePlan();
  }, [generatePlan]);

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
      installSpecificationAndReplan,
      cancelRun,
      goTo: (step: JourneyStepId) => dispatch({ type: 'step.go', step }),
      toggleEnvironment: (id: string) => dispatch({ type: 'environment.toggle', id }),
      setReinstall: (value: boolean) => dispatch({ type: 'reinstall.set', value }),
      setUpdateSpecification: (value: boolean) => dispatch({ type: 'update.set', value }),
      setInstallSpecification: (value: boolean) => dispatch({ type: 'install.set', value }),
      clearComponentRequest: () => dispatch({ type: 'request.clear' }),
      toggleChange: (id: string) => dispatch({ type: 'change.toggle', id }),
      setChanges: (ids: readonly string[], approved: boolean) => dispatch({ type: 'changes.set', ids, approved }),
      toggleCommand: (id: string) => dispatch({ type: 'command.toggle', id }),
      setResolution: (id: string, value: ConflictResolution) => dispatch({ type: 'resolution.set', id, value })
    }
  };
}

export type ConfigureJourney = ReturnType<typeof useConfigureJourney>;
