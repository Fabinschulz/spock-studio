import type { JourneyState, JourneyStepId } from '../types/journey';

/**
 * O que da jornada guardada dá para retomar quando a pessoa volta ao Configurar. `null` quando nada pode ser retomado.
 *
 * Retomar o plano é seguro: ao aplicar, o motor replaneja e recusa com `STALE_PLAN` se o projeto mudou desde que o plano
 * foi gerado, sem gravar nada. Duas coisas não sobrevivem à saída da tela e por isso são tratadas aqui:
 *
 * - **Execução em andamento.** O acompanhamento acaba com a tela. Retomar mostraria "executando" para sempre, então a
 *   jornada recomeça (a execução continua no motor e aparece nas revisões do Harness).
 * - **Leitura ou plano "carregando".** A resposta se perdeu junto com a tela. Volta a "ainda não pedido", e a pessoa
 *   pede de novo.
 */
export function restorableJourney(saved: JourneyState): JourneyState | null {
  if (saved.run !== null && saved.run.error === null) return null;

  const state: JourneyState = {
    ...saved,
    // A etapa Plano juntou-se à Revisão: uma jornada guardada nela reabre na Revisão.
    step: (saved.step as string) === 'plan' ? 'review' : saved.step,
    // Guardada antes desta escolha existir.
    installSpecification: saved.installSpecification ?? false,
    discovery: saved.discovery.status === 'loading' ? { status: 'idle' } : saved.discovery,
    plan: saved.plan.status === 'loading' ? { status: 'idle' } : saved.plan
  };
  return { ...state, step: reachableStep(state) };
}

/** A etapa mais adiantada que ainda tem o que mostrar: nenhuma etapa abre em branco por falta de dado. */
function reachableStep(state: JourneyState): JourneyStepId {
  const earlier: JourneyStepId = state.discovery.status === 'ready' ? 'model' : 'project';
  const beforeRun: JourneyStepId = state.plan.status === 'ready' ? 'review' : earlier;

  switch (state.step) {
    case 'project':
    case 'result':
      return state.step;
    case 'model':
      return earlier;
    case 'review':
      return state.plan.status === 'ready' ? state.step : earlier;
    case 'run':
      return state.run !== null ? 'run' : beforeRun;
  }
}
