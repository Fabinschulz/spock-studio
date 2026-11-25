import { describe, expect, it } from 'vitest';

import type { JourneyStepId } from '../types/journey';
import { canReopenEarlierSteps } from './step-navigation';

describe('jornada Configurar · voltar pelo stepper', () => {
  it.each<JourneyStepId>(['project', 'model', 'review'])('em %s, dá para voltar às etapas concluídas', (step) => {
    expect(canReopenEarlierSteps(step)).toBe(true);
  });

  it.each<JourneyStepId>(['run', 'result'])(
    'em %s, não dá: o plano já foi aplicado e reabri-lo levaria a um plano velho',
    (step) => {
      expect(canReopenEarlierSteps(step)).toBe(false);
    }
  );
});
