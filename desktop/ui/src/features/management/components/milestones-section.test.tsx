import milestonesFixture from '@contracts/fixtures/management/milestones.json';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { projectMilestonesSchema, type MilestoneProgress, type ProjectMilestones } from '@/shared/schema';

import type { ManagementApi } from '../service/management-api';
import { MilestonesSection } from './milestones-section';

const base = projectMilestonesSchema.parse(milestonesFixture);
const v1 = base.milestones[0]!;

const marco = (overrides: Partial<MilestoneProgress>): MilestoneProgress => ({ ...v1, ...overrides });

const renderWith = (value: ProjectMilestones) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <MilestonesSection management={{ milestones: async () => value } as unknown as ManagementApi} projectId="p-1" />
      </MemoryRouter>
    </AppProviders>
  );

const row = async (name: string) => (await screen.findByText(name)).closest('li')!;

describe('Marcos · situação', () => {
  it.each([
    ['done', 'Concluído'],
    ['on_track', 'No prazo'],
    ['at_risk', 'Em risco'],
    ['behind', 'Atrasado em relação ao prazo'],
    ['overdue', 'Prazo vencido'],
    ['no_forecast', 'Sem previsão'],
    ['no_due_date', 'Sem prazo']
  ])('o estado %s aparece como "%s", com o motivo que o motor deu', async (outlook, label) => {
    renderWith({ ...base, milestones: [marco({ outlook, outlookReason: 'Motivo do motor.' })], unassigned: [] });

    const item = await row('v1.0');

    expect(within(item).getByText(label)).toBeInTheDocument();
    expect(item).toHaveTextContent('Motivo do motor.');
  });

  it('um estado novo do motor aparece como veio, sem derrubar a seção', async () => {
    renderWith({ ...base, milestones: [marco({ outlook: 'estado-novo' })], unassigned: [] });

    expect(within(await row('v1.0')).getByText('estado-novo')).toBeInTheDocument();
  });
});

describe('Marcos · conteúdo', () => {
  it('diz quantos concluídos não contaram como entrega e usa os que contam como base', async () => {
    renderWith({
      ...base,
      milestones: [marco({ items: 12, delivered: 6, closedWithoutDelivery: 2, open: 4 })],
      unassigned: []
    });

    const item = await row('v1.0');

    expect(item).toHaveTextContent('6 de 10 itens entregues, 4 abertos; 2 concluídos sem entrega.');
  });

  it('sem previsão não mostra cenários, só o motivo', async () => {
    renderWith({
      ...base,
      milestones: [
        marco({
          outlook: 'no_forecast',
          outlookReason: 'São precisas ao menos 6 semanas de histórico; os dados cobrem 2.',
          forecast: {
            ...v1.forecast,
            scenarios: [],
            unavailable: 'São precisas ao menos 6 semanas de histórico; os dados cobrem 2.'
          }
        })
      ],
      unassigned: []
    });

    const item = await row('v1.0');

    expect(item).toHaveTextContent('São precisas ao menos 6 semanas de histórico');
    expect(item).not.toHaveTextContent('Previsão do que falta');
  });

  it('com marcos que passaram do limite diz quantos ficaram de fora', async () => {
    renderWith({ ...base, omitted: 3 });

    expect(await screen.findByText(/Há mais 3 marcos que não aparecem aqui/)).toBeInTheDocument();
  });

  it('as premissas e limites do método ficam atrás de "Como a previsão do marco é calculada"', async () => {
    renderWith(base);

    const summary = await screen.findByText('Como a previsão do marco é calculada');

    expect(summary.closest('details')).toHaveTextContent('O ritmo é o do tipo de dado inteiro');
    expect(summary.closest('details')).toHaveTextContent('Não considera a ordem em que os itens serão feitos');
  });

  it('sem nenhum marco explica de onde eles vêm e, se há itens sem marco, sugere sincronizar de novo', async () => {
    renderWith({ ...base, milestones: [], unassigned: [{ kind: 'issues', items: 5, open: 2 }] });

    const text = await screen.findByText(/Nenhum item sincronizado pertence a um marco/);

    expect(text).toHaveTextContent('milestone da issue');
    expect(text).toHaveTextContent('versão de correção');
    expect(text).toHaveTextContent('sincronize de novo');
  });
});
