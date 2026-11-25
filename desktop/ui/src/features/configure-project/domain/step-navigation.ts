import type { JourneyStepId } from '../types/journey';

/**
 * Se a pessoa pode voltar a uma etapa anterior pelo stepper. Só antes de aplicar: durante e depois da aplicação o plano
 * já foi usado, e reabri-lo levaria a aprovar de novo um plano que o motor recusaria por estar velho (`STALE_PLAN`). Dali
 * em diante, o caminho é o botão da própria etapa ("planejar de novo", "validar de novo").
 */
export function canReopenEarlierSteps(step: JourneyStepId): boolean {
  return step === 'project' || step === 'model' || step === 'review';
}
