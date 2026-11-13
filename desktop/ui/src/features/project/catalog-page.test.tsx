import catalogFixture from '@contracts/fixtures/engineering/catalog.json';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { EngineError } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { harnessCatalogSchema, type HarnessCatalog } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { CatalogPage } from './catalog-page';

const project = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const parsed = harnessCatalogSchema.parse(catalogFixture);

function clientWith(catalog: HarnessCatalog | Error) {
  const request = vi.fn(async (_method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
    if (catalog instanceof Error) throw catalog;
    return schema.parse(catalog);
  });
  return { client: { request, onEvent: vi.fn() } as unknown as EngineClient, request };
}

const renderPage = (client: EngineClient, selected: typeof project | null = project) =>
  render(
    <AppProviders>
      <MemoryRouter>
        <CatalogPage client={client} project={selected} />
      </MemoryRouter>
    </AppProviders>
  );

const card = async (title: string) => (await screen.findByRole('heading', { name: title })).closest('li')!;

describe('Catálogo do Harness', () => {
  it('pede o catálogo do projeto aberto e o do geral quando não há projeto', async () => {
    const withProject = clientWith(parsed);
    const { unmount } = renderPage(withProject.client);
    await screen.findByRole('heading', { name: 'Agente verificador' });
    expect(withProject.request).toHaveBeenCalledWith(
      'engineering.catalog',
      { projectId: project.projectId },
      expect.anything()
    );
    unmount();

    const general = clientWith({
      ...parsed,
      projectId: null,
      applied: null,
      entries: parsed.entries.map((entry) => ({ ...entry, presence: null, state: null }))
    });
    renderPage(general.client, null);
    await screen.findByRole('heading', { name: 'Agente verificador' });
    expect(general.request).toHaveBeenCalledWith('engineering.catalog', {}, expect.anything());
    expect(screen.getByText(/Nenhum projeto aberto: este é o catálogo geral/)).toBeInTheDocument();
  });

  it('agrupa os componentes por tipo, na ordem do produto', async () => {
    renderPage(clientWith(parsed).client);

    await screen.findByRole('heading', { name: 'Agentes' });

    const titles = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);
    expect(titles).toEqual(['Agentes', 'Skills', 'Regras', 'Hooks', 'Ciclo de especificação', 'Declaração do projeto']);
  });

  it('cada componente diz o que faz, e os detalhes trazem quando age, o que lê, o que altera e de onde veio', async () => {
    renderPage(clientWith(parsed).client);

    const reviewer = await card('Agente revisor de código');

    expect(reviewer).toHaveTextContent('Revisa uma mudança de código');
    expect(within(reviewer).getByText('Quando age').nextElementSibling).toHaveTextContent(
      'Quando você pede uma revisão'
    );
    expect(within(reviewer).getByText('O que lê').nextElementSibling).toHaveTextContent('A mudança a revisar');
    expect(within(reviewer).getByText('O que pode alterar').nextElementSibling).toHaveTextContent(
      'Nada. Só lê o projeto'
    );
    expect(within(reviewer).getByText('De onde veio').nextElementSibling).toHaveTextContent(
      'Adaptado do exemplo de referência'
    );
    expect(within(reviewer).getByText('Onde fica no projeto').nextElementSibling).toHaveTextContent(
      '.claude/agents/code-reviewer.md'
    );
  });

  it('mostra as ferramentas do agente, cada uma com o que quer dizer', async () => {
    renderPage(clientWith(parsed).client);

    const reviewer = await card('Agente revisor de código');
    const tools = within(reviewer).getByText('Ferramentas').nextElementSibling!;

    expect(tools).toHaveTextContent('Read: ler arquivos');
    expect(tools).toHaveTextContent('Bash: rodar comandos no terminal');
    expect(within(await card('Regras gerais')).queryByText('Ferramentas')).toBeNull();
  });

  it('diz se cada componente está aplicado, dispensado ou não aplicado, e o estado dos arquivos', async () => {
    renderPage(clientWith(parsed).client);

    expect(within(await card('Agente Harness Engineer')).getByText('Aplicado')).toBeInTheDocument();
    const verifier = await card('Agente verificador');
    expect(within(verifier).getByText('Dispensado em mariliz.yaml')).toBeInTheDocument();
    expect(within(verifier).getByText('Os arquivos saem na próxima aplicação')).toBeInTheDocument();
    expect(
      within(await card('Regras da stack JavaScript e TypeScript')).getByText('Não aplicado neste projeto')
    ).toBeInTheDocument();
  });

  it('um componente modificado à mão mostra o estado ao lado de aplicado', async () => {
    const modified: HarnessCatalog = {
      ...parsed,
      entries: parsed.entries.map((entry) =>
        entry.sheet.id === 'mariliz.agent.code-reviewer' ? { ...entry, presence: 'applied', state: 'modified' } : entry
      )
    };
    renderPage(clientWith(modified).client);

    const reviewer = await card('Agente revisor de código');

    expect(within(reviewer).getByText('Aplicado')).toBeInTheDocument();
    expect(within(reviewer).getByText('Modificado à mão')).toBeInTheDocument();
  });

  it('projeto sem Harness aplicado manda para Configurar e não há editor na tela', async () => {
    renderPage(
      clientWith({
        ...parsed,
        applied: false,
        entries: parsed.entries.map((entry) => ({ ...entry, presence: 'not_applied', state: null }))
      }).client
    );

    expect(await screen.findByText(/ainda não tem o Harness aplicado/)).toBeInTheDocument();
    expect(
      screen.getAllByRole('link', { name: 'Configurar' }).every((link) => link.getAttribute('href') === '/configurar')
    ).toBe(true);
    expect(screen.queryByRole('button', { name: /Editar|Salvar|Aplicar/ })).toBeNull();
  });

  it('uma regra do projeto aparece com a ficha genérica e um tipo novo do motor aparece depois dos conhecidos', async () => {
    const custom = {
      sheet: {
        ...parsed.entries[0]!.sheet,
        id: 'mariliz.rule.custom.pagamentos',
        kind: 'regra-nova',
        title: 'Regra do projeto: pagamentos'
      },
      presence: 'applied',
      state: 'intact'
    };
    renderPage(clientWith({ ...parsed, entries: [...parsed.entries, custom] }).client);

    expect(await card('Regra do projeto: pagamentos')).toBeInTheDocument();
    const titles = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);
    expect(titles.at(-1)).toBe('regra-nova');
  });

  it('se o motor recusa mostra o motivo', async () => {
    renderPage(clientWith(new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.')).client);

    expect(await screen.findByRole('alert')).toHaveTextContent('O projeto não foi autorizado.');
  });

  it('não tem violações de acessibilidade', async () => {
    const { container } = renderPage(clientWith(parsed).client);
    await screen.findByRole('heading', { name: 'Agentes' });

    await expectNoA11yViolations(container);
  });
});
