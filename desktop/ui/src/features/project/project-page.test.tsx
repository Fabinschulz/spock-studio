import type { EngineClient } from '@/shared/api';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectPage } from './project-page';

const project = { projectId: 'p-aaaaaaaaaaaa', displayName: 'Loja', displayPath: '~\\Loja' };

const item = (name: string, managed = true) => ({ name, path: `.claude/${name}`, managed, issue: null });

const discovery = {
  projectId: project.projectId,
  projectName: 'Loja',
  stacks: [{ id: 'dotnet', name: '.NET', root: '.', version: '10.0', confidence: 'high', evidence: [], notes: [] }],
  tooling: [{ kind: 'test', name: 'dotnet test', command: 'dotnet test', evidence: [] }],
  aiEnvironments: [],
  inventory: { agents: [item('revisor', false)], skills: [], hooks: [], rules: [], mcpServers: [], evals: [] },
  specification: {
    state: 'not_installed',
    version: null,
    lockedHash: null,
    computedHash: null,
    versionTested: false,
    problems: []
  },
  git: { isRepository: true, hasUncommittedChanges: false, changedPaths: [] },
  tools: { git: null, node: null, python: null, pythonCommand: null },
  hasDeclaredConfig: false,
  warnings: []
};

function clientWith(runs: unknown[]): EngineClient {
  return {
    request: vi.fn(async (method: string, _params: unknown, schema: { parse(value: unknown): unknown }) =>
      schema.parse(method === 'harness.runs' ? { runs } : discovery)
    ),
    onEvent: vi.fn()
  } as unknown as EngineClient;
}

const renderPage = (client: EngineClient, selected: typeof project | null = project) =>
  render(
    <MemoryRouter>
      <ProjectPage client={client} project={selected} />
    </MemoryRouter>
  );

describe('ProjectPage', () => {
  it('mostra o inventário, a stack e as execuções reais do projeto', async () => {
    const client = clientWith([
      {
        runId: 'run-aaaaaaaaaaaa',
        kind: 'apply',
        state: 'completed',
        startedAt: '2026-11-04T10:00:00Z',
        changesApplied: true,
        reverted: false
      }
    ]);
    renderPage(client);

    expect(await screen.findByText('revisor')).toBeTruthy();
    expect(screen.getByText(/não gerenciado/)).toBeTruthy();
    expect(screen.getByText('.NET')).toBeTruthy();
    expect(screen.getByText('dotnet test')).toBeTruthy();
    expect(screen.getByText('Configuração aplicada')).toBeTruthy();
    expect(screen.getByText(/alterações aplicadas/)).toBeTruthy();
    expect(client.request).toHaveBeenCalledWith('harness.runs', { projectId: project.projectId }, expect.anything());
  });

  it('sem execuções, diz isso em vez de mostrar uma lista vazia', async () => {
    renderPage(clientWith([]));
    expect(await screen.findByText('Nenhuma execução neste projeto ainda.')).toBeTruthy();
  });

  it('erro do motor vira aviso, não tela quebrada', async () => {
    const client = {
      request: vi.fn(async () => Promise.reject(new Error('falhou'))),
      onEvent: vi.fn()
    } as unknown as EngineClient;
    renderPage(client);
    expect((await screen.findByRole('alert')).textContent).toBeTruthy();
  });

  it('sem projeto selecionado, manda escolher na tela inicial', () => {
    renderPage(clientWith([]), null);
    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeTruthy();
  });
});
