import { describe, expect, it } from 'vitest';

import type { RiskChange } from '@/shared/schema';

import { describeRiskChange } from './risk-history-text';

const change = (overrides: Partial<RiskChange>): RiskChange => ({
  at: '2026-11-01T10:00:00+00:00',
  kind: 'appeared',
  from: null,
  to: 'high',
  itemsAdded: [],
  itemsRemoved: [],
  ...overrides
});

describe('texto da trajetória de um risco', () => {
  it('um risco que já estava ativo no primeiro registro não diz desde quando', () => {
    expect(describeRiskChange(change({ kind: 'observed', to: 'medium' }))).toBe(
      'Já estava ativo no primeiro registro, com gravidade média.'
    );
  });

  it('apareceu com a gravidade', () => {
    expect(describeRiskChange(change({ kind: 'appeared', to: 'low' }))).toBe('Apareceu com gravidade baixa.');
  });

  it('subiu e baixou, dizendo de onde para onde e os itens que entraram e saíram', () => {
    expect(describeRiskChange(change({ kind: 'raised', from: 'medium', to: 'high', itemsAdded: ['3', '4'] }))).toBe(
      'Subiu de média para alta; entraram #3, #4.'
    );
    expect(describeRiskChange(change({ kind: 'lowered', from: 'high', to: 'medium', itemsRemoved: ['1'] }))).toBe(
      'Baixou de alta para média; saíram #1.'
    );
  });

  it('sem mudança nos itens a frase termina na gravidade', () => {
    expect(describeRiskChange(change({ kind: 'raised', from: 'low', to: 'medium' }))).toBe(
      'Subiu de baixa para média.'
    );
  });

  it('mudança só nos itens diz o que entrou e o que saiu', () => {
    expect(describeRiskChange(change({ kind: 'items_changed', itemsAdded: ['3'], itemsRemoved: ['2'] }))).toBe(
      'Mudaram os itens: entraram #3; saíram #2.'
    );
  });

  it('resolvido diz que deixou de disparar', () => {
    expect(describeRiskChange(change({ kind: 'resolved', from: 'high', to: null }))).toBe(
      'Resolvido: deixou de disparar.'
    );
  });

  it('um tipo novo do motor aparece como veio e uma gravidade nova também', () => {
    expect(describeRiskChange(change({ kind: 'reaberto' }))).toBe('reaberto');
    expect(describeRiskChange(change({ kind: 'appeared', to: 'critica' }))).toBe('Apareceu com gravidade critica.');
  });
});
