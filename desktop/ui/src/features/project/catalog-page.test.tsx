import catalogFixture from '@contracts/fixtures/engineering/catalog.json';
import mcpFixture from '@contracts/fixtures/engineering/mcp.json';
import policyFixture from '@contracts/fixtures/engineering/policy.json';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { EngineError } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import {
  agentPolicySchema,
  harnessCatalogSchema,
  mcpInventorySchema,
  type AgentPolicy,
  type HarnessCatalog,
  type McpInventory
} from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { CatalogPage } from './catalog-page';

const project = { projectId: 'p-000000000001', displayName: 'Loja', displayPath: '~\\Loja' };
const parsed = harnessCatalogSchema.parse(catalogFixture);
const parsedMcp = mcpInventorySchema.parse(mcpFixture);
const parsedPolicy = agentPolicySchema.parse(policyFixture);

/** O catálogo, a política e o inventário de MCP são três pedidos: cada um responde (ou falha) por conta própria. */
function clientWith(
  catalog: HarnessCatalog | Error,
  mcp: McpInventory | Error = parsedMcp,
  policy: AgentPolicy | Error = parsedPolicy
) {
  const request = vi.fn(async (method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
    const answer = method === 'engineering.mcp' ? mcp : method === 'engineering.policy' ? policy : catalog;
    if (answer instanceof Error) throw answer;
    return schema.parse(answer);
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

    await screen.findByRole('heading', { name: 'Agentes (assistentes de IA)' });

    const titles = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);
    expect(titles).toEqual([
      'Agentes (assistentes de IA)',
      'Skills (instruções prontas)',
      'Regras',
      'Hooks (checagens automáticas)',
      'Ciclo de especificação',
      'Declaração do projeto',
      'O que o hook de política faz',
      'Servidores MCP do projeto'
    ]);
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
    expect(titles.at(-1)).toBe('Servidores MCP do projeto');
    expect(titles.at(-3)).toBe('regra-nova');
  });

  it('se o motor recusa mostra o motivo', async () => {
    renderPage(clientWith(new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.')).client);

    expect(await screen.findByRole('alert')).toHaveTextContent('O projeto não foi autorizado.');
  });

  describe('política do hook', () => {
    it('mostra o que bloqueia, o que pergunta e o que libera, em palavras simples', async () => {
      renderPage(clientWith(parsed).client);

      const blocks = await screen.findByRole('list', { name: 'Bloqueia' });
      expect(blocks).toHaveTextContent('Formatar um disco (mkfs).');
      expect(blocks).toHaveTextContent('Forçar o envio (git push --force) para main ou master.');
      expect(screen.getByRole('list', { name: 'Pergunta a você antes' })).toHaveTextContent('git reset --hard');
      expect(screen.getByRole('list', { name: 'Libera, como exceção' })).toHaveTextContent('.mariliz/proposal.json');
    });

    it('diz o que o hook vigia e que ele só lê', async () => {
      renderPage(clientWith(parsed).client);

      const intro = await screen.findByText(/confere o que o Claude Code vai fazer, antes de acontecer/);
      expect(intro).toHaveTextContent(
        'rodar comandos no terminal, gravar arquivos, editar arquivos e editar cadernos de notebook.'
      );
      expect(intro).toHaveTextContent('Ele só lê');
    });

    it('diz o que a política não garante', async () => {
      renderPage(clientWith(parsed).client);

      const limits = await screen.findByRole('list', { name: 'Limites da política' });
      expect(limits).toHaveTextContent('não uma barreira de segurança');
      expect(limits).toHaveTextContent('não passa pelo hook');
    });

    it('a política aparece também sem projeto aberto', async () => {
      renderPage(clientWith({ ...parsed, projectId: null, applied: null }).client, null);

      expect(await screen.findByRole('list', { name: 'Bloqueia' })).toBeInTheDocument();
    });

    it('uma decisão nova do script aparece depois das conhecidas, com o nome que veio', async () => {
      const extra: AgentPolicy = {
        ...parsedPolicy,
        rules: [...parsedPolicy.rules, { decision: 'log', scope: 'command', plain: 'Registrar o comando.' }]
      };
      renderPage(clientWith(parsed, parsedMcp, extra).client);

      expect(await screen.findByRole('list', { name: 'log' })).toHaveTextContent('Registrar o comando.');
    });

    it('se só a política falha, o catálogo continua na tela com o motivo ao lado', async () => {
      renderPage(
        clientWith(parsed, parsedMcp, new EngineError('POLICY_UNAVAILABLE', 'A política não está disponível.')).client
      );

      expect(await screen.findByRole('heading', { name: 'Agente verificador' })).toBeInTheDocument();
      expect(await screen.findByRole('alert')).toHaveTextContent('A política não está disponível.');
    });
  });

  describe('servidores MCP do projeto', () => {
    const server = async (name: string) => (await screen.findByRole('heading', { name })).closest('li')!;

    it('pede o inventário do projeto aberto e não pede nada sem projeto', async () => {
      const withProject = clientWith(parsed);
      const { unmount } = renderPage(withProject.client);
      await screen.findByRole('heading', { name: 'github' });
      expect(withProject.request).toHaveBeenCalledWith(
        'engineering.mcp',
        { projectId: project.projectId },
        expect.anything()
      );
      unmount();

      const general = clientWith({ ...parsed, projectId: null, applied: null });
      renderPage(general.client, null);
      await screen.findByRole('heading', { name: 'Agente verificador' });
      expect(general.request.mock.calls.map((call) => call[0]).sort()).toEqual([
        'engineering.catalog',
        'engineering.policy'
      ]);
      expect(screen.queryByText('Servidores MCP do projeto')).toBeNull();
    });

    it('mostra o que cada servidor roda, em palavras simples, e de onde vem cada variável', async () => {
      renderPage(clientWith(parsed).client);

      const github = await server('github');
      expect(github).toHaveTextContent('Programa local');
      expect(github).toHaveTextContent('Roda na sua máquina o programa npx');
      expect(within(github).getByText('LOG_LEVEL').parentElement).toHaveTextContent('valor escrito no arquivo');

      const docs = await server('docs');
      expect(docs).toHaveTextContent('Servidor remoto');
      expect(docs).toHaveTextContent('Conecta no servidor mcp.exemplo.com');
      expect(within(docs).queryByText('Credencial escrita no arquivo')).toBeNull();
    });

    it('avisa de credencial escrita no arquivo, diz onde e o que fazer, sem mostrar valor algum', async () => {
      const { container } = renderPage(clientWith(parsed).client);

      const github = await server('github');
      expect(within(github).getByText('Credencial escrita no arquivo')).toBeInTheDocument();
      expect(within(github).getAllByText('env.GITHUB_TOKEN').length).toBeGreaterThan(0);
      expect(github).toHaveTextContent('revogue a credencial atual');
      expect(screen.getByText(/1 com credencial escrita no arquivo/)).toBeInTheDocument();
      expect(container.textContent).not.toMatch(/ghp_/);
    });

    it('sem credencial escrita diz que não há nenhuma', async () => {
      const clean: McpInventory = {
        ...parsedMcp,
        servers: parsedMcp.servers.map((item) => ({ ...item, literalSecrets: [] }))
      };
      renderPage(clientWith(parsed, clean).client);

      expect(await screen.findByText(/nenhuma credencial escrita no arquivo/)).toBeInTheDocument();
    });

    it('projeto sem .mcp.json e arquivo inválido têm cada um a sua mensagem', async () => {
      const none = renderPage(clientWith(parsed, { ...parsedMcp, present: false, servers: [] }).client);
      expect(await screen.findByText(/não tem \.mcp\.json: nenhum servidor MCP declarado/)).toBeInTheDocument();
      none.unmount();

      renderPage(clientWith(parsed, { ...parsedMcp, readable: false, servers: [] }).client);
      expect(await screen.findByRole('alert')).toHaveTextContent('não é um JSON válido');
    });

    it('se só o inventário falha, o catálogo continua na tela com o motivo ao lado', async () => {
      renderPage(clientWith(parsed, new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.')).client);

      expect(await screen.findByRole('heading', { name: 'Agente verificador' })).toBeInTheDocument();
      expect(await screen.findByRole('alert')).toHaveTextContent('O projeto não foi autorizado.');
    });
  });

  it('não tem violações de acessibilidade', async () => {
    const { container } = renderPage(clientWith(parsed).client);
    await screen.findByRole('heading', { name: 'Agentes (assistentes de IA)' });

    await expectNoA11yViolations(container);
  });
});
