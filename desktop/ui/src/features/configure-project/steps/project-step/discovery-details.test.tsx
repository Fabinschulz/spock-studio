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
    expect(screen.getByText(/1 agente\(s\)/)).toBeTruthy();
    expect(screen.getByText('não é um repositório')).toBeTruthy();
    expect(screen.getByText(/Python: não encontrado/)).toBeTruthy();
    expect(screen.getByText(/Node: versão 22.1.0/)).toBeTruthy();
  });

  it('sem stack nem harness, diz que o projeto começa do zero', () => {
    render(<DiscoveryDetails discovery={{ ...discovery, stacks: [], tooling: [], inventory: none }} />);

    expect(screen.getByText(/Nenhuma stack conhecida/)).toBeTruthy();
    expect(screen.getByText(/o projeto começa do zero/)).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Comandos detectados' })).toBeNull();
  });
});
