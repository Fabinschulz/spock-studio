import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ProjectPickError, type ProjectPickerPort, type RecentProject, type SelectedProject } from '../domain/types';
import { ProjectSelectionProvider, useProjectSelectionContext } from './project-selection-context';

const loja: SelectedProject = { projectId: 'p-1', displayName: 'loja', displayPath: '~', recentId: 'r-1' };
const portal: SelectedProject = { projectId: 'p-2', displayName: 'portal', displayPath: '~', recentId: 'r-2' };

function recent(id: string, overrides: Partial<RecentProject> = {}): RecentProject {
  return { id, displayName: id, displayPath: '~', lastOpenedMs: 1, available: true, ...overrides };
}

function Probe() {
  const { selected, error, pick } = useProjectSelectionContext();
  return (
    <>
      <p data-testid="selected">{selected?.displayName ?? 'nenhum'}</p>
      <p data-testid="error">{error ?? 'sem erro'}</p>
      <button type="button" onClick={() => void pick()}>
        escolher
      </button>
    </>
  );
}

function renderProvider(picker: ProjectPickerPort, restoreLast?: boolean) {
  return render(
    <ProjectSelectionProvider picker={picker} restoreLast={restoreLast}>
      <Probe />
    </ProjectSelectionProvider>
  );
}

describe('seleção de projeto · reabrir o último', () => {
  it('com a restauração ligada, reabre o recente mais novo, que o host lista primeiro', async () => {
    const openRecent = vi.fn().mockResolvedValue(loja);
    renderProvider(
      { pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent('r-1'), recent('r-2')]), openRecent },
      true
    );

    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('loja'));
    expect(openRecent).toHaveBeenCalledExactlyOnceWith('r-1');
  });

  it('sem a opção, o aplicativo abre sem projeto, como antes', async () => {
    const openRecent = vi.fn();
    const recents = vi.fn().mockResolvedValue([recent('r-1')]);
    renderProvider({ pick: vi.fn(), recents, openRecent });

    await waitFor(() => expect(recents).toHaveBeenCalled());
    expect(openRecent).not.toHaveBeenCalled();
    expect(screen.getByTestId('selected')).toHaveTextContent('nenhum');
  });

  it('não reabre uma pasta que sumiu e não finge que há projeto', async () => {
    const openRecent = vi.fn();
    const recents = vi.fn().mockResolvedValue([recent('r-1', { available: false })]);
    renderProvider({ pick: vi.fn(), recents, openRecent }, true);

    await waitFor(() => expect(recents).toHaveBeenCalled());
    expect(openRecent).not.toHaveBeenCalled();
    expect(screen.getByTestId('selected')).toHaveTextContent('nenhum');
  });

  it('se a reabertura falha, não mostra erro: a pessoa só vê o aplicativo sem projeto', async () => {
    const openRecent = vi.fn().mockRejectedValue(new ProjectPickError('PATH_NOT_FOUND', 'A pasta não existe mais.'));
    renderProvider({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent('r-1')]), openRecent }, true);

    await waitFor(() => expect(openRecent).toHaveBeenCalled());
    expect(screen.getByTestId('selected')).toHaveTextContent('nenhum');
    expect(screen.getByTestId('error')).toHaveTextContent('sem erro');
  });

  it('uma escolha da pessoa vence a restauração, mesmo que esta termine depois', async () => {
    let finishRestore: (project: SelectedProject) => void = () => undefined;
    const openRecent = vi.fn().mockReturnValue(
      new Promise<SelectedProject>((resolve) => {
        finishRestore = resolve;
      })
    );
    renderProvider(
      { pick: vi.fn().mockResolvedValue(portal), recents: vi.fn().mockResolvedValue([recent('r-1')]), openRecent },
      true
    );
    await waitFor(() => expect(openRecent).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('portal'));
    finishRestore(loja);

    // A restauração terminou depois da escolha: o projeto escolhido continua sendo o aberto.
    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('portal'));
  });
});
