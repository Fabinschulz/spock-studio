import listFixture from '@contracts/fixtures/golden/quality-list.json';
import reviewFixture from '@contracts/fixtures/golden/quality-review.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { QualityPage } from './quality-page';

const loja = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };

function fakeEngine(options: { fail?: { code: string; message: string } } = {}) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      if (method === 'quality.review.start' && options.fail)
        throw new EngineError(options.fail.code, options.fail.message);
      const reply: Record<string, unknown> = {
        'quality.review.start': reviewFixture,
        'quality.review.detail': reviewFixture,
        'quality.review.list': listFixture
      };
      return schema.parse(reply[method]);
    },
    async onEvent() {
      return () => undefined;
    }
  };
  return { client, calls };
}

function renderPage(client: EngineClient, project: typeof loja | null = loja) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <QualityPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

describe('Revisão de código', () => {
  it('pede um projeto antes de qualquer coisa', () => {
    renderPage(fakeEngine().client, null);

    expect(screen.getByRole('heading', { name: 'Revisão de código' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Revisar alterações locais' })).not.toBeInTheDocument();
  });

  it('revisa as alterações locais e separa confirmados, a conferir e recomendações, sem mostrar segredo', async () => {
    const engine = fakeEngine();
    const { container } = renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const confirmed = await screen.findByRole('region', { name: 'Problemas confirmados' });
    const toCheck = screen.getByRole('region', { name: 'A conferir' });
    expect(within(confirmed).getByText('Credencial de um provedor no código alterado')).toBeInTheDocument();
    expect(within(toCheck).getByText('Erro engolido por um tratamento vazio')).toBeInTheDocument();
    expect(screen.getAllByText('Reprovada').length).toBeGreaterThanOrEqual(1);
    expect(engine.calls.find((call) => call.method === 'quality.review.start')?.params).toEqual({
      projectId: loja.projectId,
      mode: 'working_tree'
    });
    expect(container).not.toHaveTextContent(/ghp_/);
    await expectNoA11yViolations(container);
  });

  it('diz quem avaliou e o que não foi verificado: nenhum juiz de IA rodou', async () => {
    renderPage(fakeEngine().client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const judges = await screen.findByRole('region', { name: 'Quem avaliou' });
    expect(within(judges).getByText('Revisão por modelo de IA')).toBeInTheDocument();
    expect(within(judges).getAllByText('Não executada').length).toBeGreaterThanOrEqual(3);
    const limits = screen.getByRole('region', { name: 'O que não foi verificado' });
    expect(limits).toHaveTextContent('build, testes e lint não rodaram');
  });

  it('o aviso não repete o veredito no texto', async () => {
    renderPage(fakeEngine().client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent(/^Reprovada\s*1 problema confirmado e grave/);
    expect(notice).not.toHaveTextContent('Reprovada: ');
  });

  it('um erro tira da tela o relatório anterior', async () => {
    const options: { fail?: { code: string; message: string } } = {};
    renderPage(fakeEngine(options).client);
    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));
    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();

    options.fail = { code: 'REVIEW_REF_NOT_FOUND', message: 'A referência base não existe neste repositório.' };
    await userEvent.type(screen.getByLabelText('Comparar com a base'), 'nao-existe');
    await userEvent.click(screen.getByRole('button', { name: 'Revisar a branch' }));

    expect(await screen.findByText('A referência base não existe neste repositório.')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Problemas confirmados' })).not.toBeInTheDocument();
  });

  it('a branch só é revisada com a base escrita', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);
    const submit = await screen.findByRole('button', { name: 'Revisar a branch' });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText('Comparar com a base'), 'main');
    await userEvent.click(submit);

    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'quality.review.start')?.params).toEqual({
      projectId: loja.projectId,
      mode: 'branch',
      base: 'main'
    });
  });

  it('mostra o motivo quando a pasta não é um repositório', async () => {
    const engine = fakeEngine({
      fail: { code: 'REVIEW_NOT_A_REPOSITORY', message: 'A pasta do projeto não é um repositório git.' }
    });
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: 'Revisar alterações locais' }));

    expect(await screen.findByText('A revisão não foi feita')).toBeInTheDocument();
    expect(screen.getByText(/não é um repositório git/)).toBeInTheDocument();
  });

  it('lista o histórico e abre uma revisão anterior', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await userEvent.click(await screen.findByRole('button', { name: /^Abrir a revisão de / }));

    expect(await screen.findByRole('region', { name: 'Problemas confirmados' })).toBeInTheDocument();
    expect(engine.calls.some((call) => call.method === 'quality.review.detail')).toBe(true);
  });
});
