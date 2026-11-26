import type { Discovery } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DiscoveryDetails } from './discovery-details';

const none = { agents: [], skills: [], hooks: [], rules: [], mcpServers: [], evals: [] };

const discovery: Discovery = {
  projectId: 'p-aaaaaaaaaaaa',
  projectName: 'Loja',
  stacks: [
    {
      id: 'dotnet',
      name: '.NET',
      root: '',
      version: '10.0',
      confidence: 'high',
      evidence: [{ path: 'Loja.sln', detail: null }],
      notes: []
    }
  ],
  tooling: [{ kind: 'test', name: 'dotnet test', command: 'dotnet test', evidence: [] }],
  aiEnvironments: [],
  inventory: { ...none, agents: [{ name: 'revisor', path: '.claude/agents/revisor.md', managed: false, issue: null }] },
  specification: {
    state: 'not_installed',
    version: null,
    lockedHash: null,
    computedHash: null,
    versionTested: false,
    problems: []
  },
  git: { isRepository: false, hasUncommittedChanges: null, changedPaths: [] },
  tools: { git: null, node: '22.1.0', python: null, pythonCommand: null },
  hasDeclaredConfig: false,
  declaredAiEnvironments: [],
  naming: {
    declaration: 'spock.yaml',
    lock: 'spock.lock',
    stateDirectory: '.spock',
    proposal: '.spock/proposal.json',
    proposalSchema: '.spock/schemas/harness-proposal.v1.schema.json',
    harnessEngineer: 'spock-harness-engineer',
    harnessEngineerAgent: '.claude/agents/spock-harness-engineer.md'
  },
  warnings: []
};

describe('DiscoveryDetails', () => {
  it('mostra stack, comandos e estado atual em linhas, com o que não foi encontrado dito em texto', () => {
    render(<DiscoveryDetails discovery={discovery} />);

    expect(screen.getByRole('heading', { name: 'Stack' })).toBeTruthy();
    expect(screen.getByText('.NET')).toBeTruthy();
    expect(screen.getByText(/em raiz do projeto/)).toBeTruthy();
    expect(screen.getByText('dotnet test')).toBeTruthy();
    expect(screen.getByText('Nada é executado por ter sido detectado.')).toBeTruthy();
    expect(screen.getByText(/existente/).closest('li')).toHaveTextContent(
      'Harness existente 1 agente, 0 skills, 0 regras, 0 hooks, 0 servidores MCP, 0 arquivos de eval'
    );
    expect(screen.getByText('não é um repositório')).toBeTruthy();
    expect(screen.getByText(/Python: não encontrado/)).toBeTruthy();
    expect(screen.getByText(/Node: versão 22.1.0/)).toBeTruthy();
  });

  it('os termos técnicos do estado atual levam a explicação do glossário', () => {
    render(<DiscoveryDetails discovery={discovery} />);

    for (const name of ['Harness', 'skills', 'hooks', 'servidores MCP', 'arquivos de eval']) {
      expect(screen.getByRole('button', { name })).toBeTruthy();
    }
  });

  it('separa o rótulo do comando detectado', () => {
    render(<DiscoveryDetails discovery={discovery} />);

    const command = screen.getByText('dotnet test');
    expect(command.tagName).toBe('CODE');
    expect(command.previousElementSibling).toHaveTextContent(/^Testes?$/);
  });

  it('diz o que a confiança da stack muda, e só quando não é alta', () => {
    const stack = discovery.stacks[0]!;
    const { rerender } = render(<DiscoveryDetails discovery={discovery} />);
    expect(screen.queryByText(/não muda o que entra no plano/)).toBeNull();

    rerender(
      <DiscoveryDetails discovery={{ ...discovery, stacks: [{ ...stack, version: null, confidence: 'low' }] }} />
    );
    expect(
      screen.getByText(/A versão não foi encontrada nos arquivos\. Isso não muda o que entra no plano/)
    ).toBeTruthy();

    rerender(<DiscoveryDetails discovery={{ ...discovery, stacks: [{ ...stack, confidence: 'medium' }] }} />);
    expect(screen.getByText(/deduzida do que o projeto declara/)).toBeTruthy();
  });

  it('sem ambiente de IA detectado, aponta a etapa em que se escolhe em vez de pedir a escolha aqui', () => {
    render(
      <DiscoveryDetails
        discovery={{
          ...discovery,
          warnings: [
            {
              code: 'NO_AI_ENVIRONMENT_DETECTED',
              message: 'Nenhum ambiente de IA foi detectado; selecione o que o projeto usa.',
              paths: []
            }
          ]
        }}
      />
    );

    expect(screen.queryByText(/selecione o que o projeto usa/)).toBeNull();
    expect(screen.getByText(/Você escolhe qual a equipe usa na próxima etapa, Ambiente de IA\./)).toBeTruthy();
  });

  it('sem stack nem harness, diz que o projeto começa do zero', () => {
    render(<DiscoveryDetails discovery={{ ...discovery, stacks: [], tooling: [], inventory: none }} />);

    expect(screen.getByText(/Nenhuma stack conhecida/)).toBeTruthy();
    expect(screen.getByText(/o projeto começa do zero/)).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Comandos detectados' })).toBeNull();
  });
});
