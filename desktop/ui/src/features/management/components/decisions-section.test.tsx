import decisionGetFixture from '@contracts/fixtures/management/decision-get.json';
import decisionListFixture from '@contracts/fixtures/management/decision-list.json';
import runListFixture from '@contracts/fixtures/runs/run-list.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { EngineError } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { decisionGetSchema, decisionListSchema, runListSchema, type DecisionSummary } from '@/shared/schema';

import type { ManagementApi } from '../service/management-api';
import { DecisionsSection } from './decisions-section';

const summaries = decisionListSchema.parse(decisionListFixture).decisions;
const full = decisionGetSchema.parse(decisionGetFixture).decision;
const runs = runListSchema.parse(runListFixture).runs;

function api(overrides: Partial<ManagementApi> & { listed?: DecisionSummary[] } = {}) {
  let listed = overrides.listed ?? [];
  const recordDecision = vi.fn(async () => {
    listed = summaries;
    return { decision: full, redacted: false };
  });
  const management = {
    decisions: vi.fn(async () => listed),
    decision: vi.fn(async () => full),
    recentRuns: vi.fn(async () => runs),
    recordDecision,
    ...overrides
  } as unknown as ManagementApi;
  return { management, recordDecision };
}

const renderSection = (management: ManagementApi) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <DecisionsSection management={management} projectId="p-1" />
      </MemoryRouter>
    </AppProviders>
  );

const openForm = async () => {
  await userEvent.click(await screen.findByRole('button', { name: 'Registrar uma decisão' }));
  return screen.getByRole('form', { name: 'Nova decisão' });
};

describe('Decisões · lista', () => {
  it('sem decisões diz para que serve registrar uma', async () => {
    renderSection(api().management);

    expect(await screen.findByText(/Nenhuma decisão registrada neste projeto/)).toBeInTheDocument();
  });

  it('mostra cada decisão com o que foi decidido, o motivo e as execuções relacionadas', async () => {
    renderSection(api({ listed: summaries }).management);

    const list = await screen.findByRole('list', { name: 'Decisões registradas' });

    expect(list).toHaveTextContent('Adiar a release');
    expect(list).toHaveTextContent('Adiar a release 1.0 em uma semana.');
    expect(list).toHaveTextContent('Por quê: Três itens bloqueados.');
    expect(list).toHaveTextContent(
      '2 execuções relacionadas: Configuração aplicada (Concluído), Pedido de engenharia (Concluído com limitações)'
    );
  });

  it('os fatos do dia só são buscados ao pedir, aparecem com o que ficou de fora e se escondem de novo', async () => {
    const { management } = api({ listed: summaries });
    renderSection(management);
    const toggle = await screen.findByRole('button', { name: 'Ver os fatos do dia (2)' });
    expect(management.decision).not.toHaveBeenCalled();

    await userEvent.click(toggle);

    const facts = await screen.findByRole('list', { name: 'Fatos de Adiar a release' });
    expect(facts).toHaveTextContent('Issues: 18 itens sincronizados, 2 abertos.');
    expect(screen.getByText('Nenhum código é enviado.')).toBeInTheDocument();
    expect(management.decision).toHaveBeenCalledWith('p-1', 'dec-000000000001');

    await userEvent.click(screen.getByRole('button', { name: 'Esconder os fatos do dia' }));
    expect(screen.queryByRole('list', { name: 'Fatos de Adiar a release' })).toBeNull();
  });

  it('se a decisão não abre diz o motivo e mantém o botão para tentar de novo', async () => {
    const { management } = api({
      listed: summaries,
      decision: vi.fn(async () => {
        throw new EngineError('DECISION_NOT_FOUND', 'Decisão não encontrada.');
      })
    });
    renderSection(management);

    await userEvent.click(await screen.findByRole('button', { name: 'Ver os fatos do dia (2)' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Decisão não encontrada.');
    expect(screen.getByRole('button', { name: 'Ver os fatos do dia (2)' })).toBeInTheDocument();
  });

  it('se a lista não carrega diz o motivo', async () => {
    renderSection(
      api({
        decisions: vi.fn(async () => {
          throw new EngineError('INTERNAL_ERROR', 'Falha interna.');
        })
      }).management
    );

    expect(await screen.findByRole('alert')).toHaveTextContent('Falha interna.');
  });
});

describe('Decisões · registro', () => {
  it('o formulário só abre pelo botão e avisa que não se edita nem se apaga', async () => {
    renderSection(api().management);
    await screen.findByText(/Nenhuma decisão registrada/);
    expect(screen.queryByRole('form', { name: 'Nova decisão' })).toBeNull();

    const form = await openForm();

    expect(form).toHaveTextContent('não pode ser editada nem apagada');
    expect(form).toHaveTextContent('Os fatos que o motor tem hoje ficam guardados com a decisão');
  });

  it('só habilita registrar com título e decisão, e limita o tamanho dos campos', async () => {
    renderSection(api().management);
    const form = await openForm();
    const submit = within(form).getByRole('button', { name: 'Registrar decisão' });
    expect(submit).toBeDisabled();
    expect(submit).toHaveAccessibleDescription('Preencha o título e o que foi decidido.');

    await userEvent.type(within(form).getByRole('textbox', { name: 'Título' }), 'Adiar');
    expect(submit).toBeDisabled();
    await userEvent.type(within(form).getByRole('textbox', { name: 'O que foi decidido' }), 'Adiar a release.');

    expect(submit).toBeEnabled();
    expect(within(form).getByRole('textbox', { name: 'Título' })).toHaveAttribute('maxlength', '120');
    expect(within(form).getByRole('textbox', { name: 'O que foi decidido' })).toHaveAttribute('maxlength', '1000');
    expect(within(form).getByLabelText('Por quê (opcional)')).toHaveAttribute('maxlength', '2000');
  });

  it('oferece as execuções recentes para relacionar e registra com as escolhidas', async () => {
    const { management, recordDecision } = api();
    renderSection(management);
    const form = await openForm();

    await userEvent.type(within(form).getByRole('textbox', { name: 'Título' }), 'Adiar a release');
    await userEvent.type(within(form).getByRole('textbox', { name: 'O que foi decidido' }), 'Adiar em uma semana.');
    await userEvent.type(within(form).getByLabelText('Por quê (opcional)'), 'Itens bloqueados.');
    await userEvent.click(await within(form).findByRole('checkbox', { name: /Pedido de engenharia · Falhou/ }));
    await userEvent.click(within(form).getByRole('button', { name: 'Registrar decisão' }));

    await waitFor(() =>
      expect(recordDecision).toHaveBeenCalledWith({
        projectId: 'p-1',
        title: 'Adiar a release',
        decision: 'Adiar em uma semana.',
        rationale: 'Itens bloqueados.',
        runIds: ['run-2']
      })
    );
    expect(await screen.findByText(/Decisão registrada com os fatos de hoje/)).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Nova decisão' })).toBeNull();
    expect(await screen.findByRole('list', { name: 'Decisões registradas' })).toHaveTextContent('Adiar a release');
    expect(management.decisions).toHaveBeenCalledTimes(2);
  });

  it('avisa quando o motor substituiu um trecho que parecia segredo', async () => {
    const { management } = api({ recordDecision: vi.fn(async () => ({ decision: full, redacted: true })) });
    renderSection(management);
    const form = await openForm();
    await userEvent.type(within(form).getByRole('textbox', { name: 'Título' }), 'Chave');
    await userEvent.type(within(form).getByRole('textbox', { name: 'O que foi decidido' }), 'Usar a chave X.');

    await userEvent.click(within(form).getByRole('button', { name: 'Registrar decisão' }));

    expect(
      await screen.findByText(/Um trecho do texto parecia um segredo e foi substituído antes de guardar/)
    ).toBeInTheDocument();
  });

  it('se o motor recusa mostra o motivo e mantém o que foi digitado', async () => {
    const { management } = api({
      recordDecision: vi.fn(async () => {
        throw new EngineError('DECISION_RUN_NOT_FOUND', 'Uma das execuções informadas não existe neste projeto.');
      })
    });
    renderSection(management);
    const form = await openForm();
    await userEvent.type(within(form).getByRole('textbox', { name: 'Título' }), 'Adiar');
    await userEvent.type(within(form).getByRole('textbox', { name: 'O que foi decidido' }), 'Adiar em uma semana.');

    await userEvent.click(within(form).getByRole('button', { name: 'Registrar decisão' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent(
      'Uma das execuções informadas não existe neste projeto.'
    );
    expect(within(form).getByRole('textbox', { name: 'Título' })).toHaveValue('Adiar');
    expect(within(form).getByRole('textbox', { name: 'O que foi decidido' })).toHaveValue('Adiar em uma semana.');
  });

  it('um projeto sem execuções diz isso no lugar da lista de execuções', async () => {
    renderSection(api({ recentRuns: vi.fn(async () => []) }).management);
    const form = await openForm();

    expect(await within(form).findByText('Este projeto ainda não tem execuções.')).toBeInTheDocument();
  });
});
