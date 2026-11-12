import riskHistoryFixture from '@contracts/fixtures/management/risk-history.json';
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { projectRiskHistorySchema, type ProjectRiskHistory } from '@/shared/schema';

import type { ManagementApi } from '../service/management-api';
import { RiskHistorySection } from './risk-history-section';

const base = projectRiskHistorySchema.parse(riskHistoryFixture);

const renderWith = (value: ProjectRiskHistory) =>
  render(
    <AppProviders>
      <RiskHistorySection management={{ riskHistory: async () => value } as unknown as ManagementApi} projectId="p-1" />
    </AppProviders>
  );

const trail = async (title: string) => (await screen.findByText(title)).closest('li')!;

describe('Histórico dos riscos', () => {
  it('sem nenhum registro diz quando o primeiro será feito, em vez de mostrar uma lista vazia', async () => {
    renderWith({ ...base, since: null, lastCheckedAt: null, snapshots: 0, trails: [] });

    expect(await screen.findByText(/Ainda não há histórico/)).toHaveTextContent('todas as fontes completas');
  });

  it('com registros mas sem risco disparado diz isso', async () => {
    renderWith({ ...base, trails: [] });

    expect(
      await screen.findByText(/Nenhuma regra de risco disparou nos registros feitos até agora/)
    ).toBeInTheDocument();
  });

  it('diz quantos registros há, desde quando e a base: o que não se sabe do que veio antes', async () => {
    renderWith(base);

    expect(await screen.findByText(/3 registros, desde/)).toBeInTheDocument();
    expect(screen.getByText(/O que aconteceu antes do primeiro registro/)).toBeInTheDocument();
  });

  it('um risco ativo mostra a gravidade atual e um resolvido mostra que foi resolvido', async () => {
    renderWith(base);

    expect(within(await trail('Itens bloqueados')).getByText('Ativo, gravidade baixa')).toBeInTheDocument();
    expect(within(await trail('Itens com prazo vencido')).getByText(/^Resolvido(?!:)/)).toBeInTheDocument();
  });

  it('a trajetória, atrás de "Trajetória", conta cada mudança em ordem', async () => {
    renderWith(base);

    const item = await trail('Itens bloqueados');
    const list = within(item).getByRole('list', { name: 'Mudanças de Itens bloqueados' });

    expect(within(item).getByText('Trajetória (3 registros)')).toBeInTheDocument();
    expect(list.children).toHaveLength(3);
    expect(list.children[0]).toHaveTextContent('Já estava ativo no primeiro registro, com gravidade baixa.');
    expect(list.children[1]).toHaveTextContent('Subiu de baixa para média; entraram #2.');
    expect(list.children[2]).toHaveTextContent('Baixou de média para baixa; saíram #1.');
  });

  it('avisa quando há riscos no histórico além do limite', async () => {
    renderWith({ ...base, omitted: 4 });

    expect(await screen.findByText(/Há mais 4 riscos no histórico que não aparecem aqui/)).toBeInTheDocument();
  });
});
