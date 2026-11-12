import { describe, expect, it } from 'vitest';

import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import { discoverySchema, planSchema } from '@/shared/schema';

import { initialJourneyState } from '../constants/initial-journey-state';
import type { JourneyState } from '../types/journey';
import { journeyReducer } from './journey-reducer';
import { restorableJourney } from './restore-journey';

const discovery = discoverySchema.parse(discoveryFixture);
const plan = planSchema.parse(planFixture);

const discovered = (): JourneyState => journeyReducer(initialJourneyState, { type: 'discover.ok', discovery });
const planned = (): JourneyState => journeyReducer(discovered(), { type: 'plan.ok', plan });
const running = (): JourneyState => journeyReducer(planned(), { type: 'run.start', kind: 'apply', at: 1 });

describe('jornada Configurar · o que dá para retomar', () => {
  it('um plano pronto e as aprovações voltam como estavam', () => {
    const saved = { ...planned(), step: 'review' as const };

    expect(restorableJourney(saved)).toEqual(saved);
  });

  it('o resultado de uma aplicação terminada volta', () => {
    const saved = { ...planned(), step: 'result' as const };

    expect(restorableJourney(saved)?.step).toBe('result');
  });

  it('execução em andamento não volta: o acompanhamento acabou junto com a tela', () => {
    expect(running().run).not.toBeNull();

    expect(restorableJourney(running())).toBeNull();
  });

  it('execução que falhou volta com o erro à vista, em vez de sumir', () => {
    const failed = journeyReducer(running(), { type: 'run.error', message: 'O instalador falhou.' });

    const restored = restorableJourney(failed);

    expect(restored?.step).toBe('run');
    expect(restored?.run?.error).toBe('O instalador falhou.');
  });

  it('uma leitura que estava carregando volta a "ainda não pedida", sem ficar presa', () => {
    const loading = journeyReducer(initialJourneyState, { type: 'discover.start' });

    const restored = restorableJourney(loading);

    expect(restored?.discovery).toEqual({ status: 'idle' });
    expect(restored?.step).toBe('project');
  });

  it('um plano que estava carregando volta a "ainda não pedido" e a etapa recua para onde ele se pede', () => {
    const loading = journeyReducer({ ...discovered(), step: 'model' }, { type: 'plan.start' });

    const restored = restorableJourney(loading);

    expect(restored?.plan).toEqual({ status: 'idle' });
    expect(restored?.step).toBe('model');
  });

  it('nenhuma etapa volta em branco: sem o dado que ela mostra, recua para a anterior', () => {
    expect(restorableJourney({ ...initialJourneyState, step: 'model' })?.step).toBe('project');
    expect(restorableJourney({ ...discovered(), step: 'plan' })?.step).toBe('model');
    expect(restorableJourney({ ...discovered(), step: 'review' })?.step).toBe('model');
  });

  it('um erro de leitura continua à vista para a pessoa tentar de novo', () => {
    const failed = journeyReducer(initialJourneyState, { type: 'discover.error', message: 'Sem acesso.' });

    expect(restorableJourney(failed)?.discovery).toEqual({ status: 'error', message: 'Sem acesso.' });
  });
});
