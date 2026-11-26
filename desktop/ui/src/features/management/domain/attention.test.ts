import { describe, expect, it } from 'vitest';

import metricsFixture from '@contracts/fixtures/management/metrics.json';
import risksFixture from '@contracts/fixtures/management/risks.json';
import { projectMetricsSchema, projectRisksSchema } from '@/shared/schema';

import { summarizeAttention } from './attention';

const metrics = projectMetricsSchema.parse(metricsFixture);
const risks = projectRisksSchema.parse(risksFixture);
const DAY = 86_400_000;
const now = Date.parse('2026-11-20T12:00:00Z');

const withOverdue = (value: number | null) => ({
  ...metrics,
  kinds: metrics.kinds.map((kind, index) => ({
    ...kind,
    overdue: index === 0 ? { ...kind.overdue, value } : kind.overdue
  }))
});

describe('o que pede atenção', () => {
  it('conta os riscos ativos e quantos são de gravidade alta', () => {
    const { items } = summarizeAttention({ risks, metrics, oldestReadAt: null, now });

    expect(items[0]).toEqual({ id: 'risks', text: '3 riscos ativos, 1 de gravidade alta.' });
  });

  it('sem risco ativo, não fala de risco', () => {
    const { items } = summarizeAttention({ risks: { ...risks, risks: [] }, metrics, oldestReadAt: null, now });

    expect(items.map((item) => item.id)).not.toContain('risks');
  });

  it('soma o prazo vencido dos tipos que o informam e ignora o que não tem base', () => {
    const { items } = summarizeAttention({
      risks: { ...risks, risks: [] },
      metrics: withOverdue(2),
      oldestReadAt: null,
      now
    });
    expect(items).toContainEqual({ id: 'overdue', text: '2 itens abertos com prazo vencido.' });

    const none = summarizeAttention({
      risks: { ...risks, risks: [] },
      metrics: withOverdue(null),
      oldestReadAt: null,
      now
    });
    expect(none.items.map((item) => item.id)).not.toContain('overdue');
  });

  it('avisa de dados incompletos e de dados lidos há mais de sete dias', () => {
    const { items } = summarizeAttention({
      risks: { ...risks, risks: [] },
      metrics: { ...metrics, complete: false },
      oldestReadAt: new Date(now - 10 * DAY).toISOString(),
      now
    });

    expect(items.map((item) => item.id)).toEqual(['incomplete', 'stale']);
    expect(items[1]!.text).toBe('Dados lidos há 10 dias.');
  });

  it('dados lidos há sete dias ou menos não são antigos', () => {
    const { items } = summarizeAttention({
      risks: { ...risks, risks: [] },
      metrics,
      oldestReadAt: new Date(now - 7 * DAY).toISOString(),
      now
    });

    expect(items).toEqual([]);
  });

  it('sem nenhum dado sincronizado, diz que falta dado em vez de que nada pede atenção', () => {
    const empty = summarizeAttention({
      risks: { ...risks, hasData: false, risks: [] },
      metrics: { ...metrics, kinds: [] },
      oldestReadAt: null,
      now
    });

    expect(empty.hasData).toBe(false);
    expect(summarizeAttention({ risks, metrics, oldestReadAt: null, now }).hasData).toBe(true);
  });
});
