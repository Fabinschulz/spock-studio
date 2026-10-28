import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import {
  formatRecentDate,
  ProjectPickError,
  ProjectSelectionProvider,
  RecentProjects,
  useProjectSelectionContext,
  type ProjectPickerPort,
  type RecentProject
} from '.';
import { ProjectSelection } from './project-selection';

const NOW = new Date(2026, 9, 27, 18, 0).getTime();
const at = (daysAgo: number, hour: number, minute: number) => {
  const d = new Date(NOW);
  d.setDate(d.getDate() - daysAgo);
  d.setHours(hour, minute, 0, 0);
  return d.getTime();
};

const recent = (over: Partial<RecentProject> = {}): RecentProject => ({
  id: 'r-1',
  displayName: 'easymart-web',
  displayPath: '~\\…\\easymart-web',
  lastOpenedMs: at(0, 14, 32),
  available: true,
  ...over
});

const opened = { projectId: 'p-1', displayName: 'easymart-web', displayPath: '~\\…\\easymart-web' };

/** A escolha da pasta (com seus avisos) e a lista de recentes, ligadas ao mesmo contexto, como na jornada. */
function Harness() {
  const { recents, selected, busy, openRecent } = useProjectSelectionContext();
  return (
    <>
      <ProjectSelection />
      <RecentProjects
        recents={recents}
        selectedName={selected?.displayName}
        busy={busy}
        onOpen={(id) => void openRecent(id)}
      />
    </>
  );
}

function renderSelection(port: ProjectPickerPort) {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={port}>
        <MemoryRouter>
          <Harness />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

describe('formatRecentDate', () => {
  it('hoje, ontem e data completa, sempre com a hora', () => {
    expect(formatRecentDate(at(0, 14, 32), NOW)).toBe('Hoje · 14:32');
    expect(formatRecentDate(at(1, 16, 20), NOW)).toBe('Ontem · 16:20');
    expect(formatRecentDate(at(4, 10, 15), NOW)).toBe('23/10/2026 · 10:15');
  });

  it('a virada do dia conta pelo calendário, não por 24 horas', () => {
    const justAfterMidnight = new Date(2026, 9, 27, 0, 5).getTime();
    const lateYesterday = new Date(2026, 9, 26, 23, 50).getTime();

    expect(formatRecentDate(lateYesterday, justAfterMidnight)).toBe('Ontem · 23:50');
  });
});

describe('Projetos recentes', () => {
  it('lista o que o host informou, do mais recente ao mais antigo, com a data', async () => {
    renderSelection({
      pick: vi.fn(),
      recents: vi
        .fn()
        .mockResolvedValue([
          recent(),
          recent({ id: 'r-2', displayName: 'empreganet-api', lastOpenedMs: at(1, 16, 20) })
        ])
    });

    const list = within(await screen.findByRole('region', { name: 'Projetos recentes' }));
    const items = list.getAllByRole('button');
    expect(items[0]).toHaveTextContent('easymart-web');
    expect(items[0]).toHaveTextContent('Hoje · 14:32');
    expect(items[1]).toHaveTextContent('empreganet-api');
    expect(items[1]).toHaveTextContent('Ontem · 16:20');
  });

  it('sem recentes (ou fora do aplicativo desktop) a seção nem aparece', async () => {
    renderSelection({ pick: vi.fn() });

    expect(screen.queryByRole('heading', { name: 'Projetos recentes' })).not.toBeInTheDocument();
  });

  it('escolher um recente seleciona a pasta pelo id, sem enviar caminho, e não inicia a análise', async () => {
    const openRecent = vi.fn().mockResolvedValue(opened);
    renderSelection({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent()]), openRecent });

    await userEvent.click(await screen.findByRole('button', { name: /easymart-web/ }));

    expect(openRecent).toHaveBeenCalledWith('r-1');
    expect(
      await screen.findByText('Os arquivos permanecem no computador. A análise inicial é somente de leitura.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Analisar projeto/ })).not.toBeInTheDocument();
  });

  it('pasta que não existe mais fica à vista, indisponível, com o motivo dito, e não pode ser aberta', async () => {
    const openRecent = vi.fn();
    renderSelection({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent({ available: false })]), openRecent });

    const card = await screen.findByRole('button', { name: /easymart-web/ });

    expect(card).toHaveAttribute('aria-disabled', 'true');
    expect(card).toHaveAccessibleDescription(/Indisponível: a pasta não existe mais/);
    await userEvent.click(card);
    expect(openRecent).not.toHaveBeenCalled();
  });

  it('falha ao reabrir é anunciada com a causa do host e a lista é atualizada', async () => {
    const recents = vi.fn().mockResolvedValue([recent()]);
    const openRecent = vi
      .fn()
      .mockRejectedValue(new ProjectPickError('PATH_NOT_FOUND', 'A pasta deste projeto não existe mais.'));
    renderSelection({ pick: vi.fn(), recents, openRecent });

    await userEvent.click(await screen.findByRole('button', { name: /easymart-web/ }));

    expect(await screen.findByRole('alert')).toHaveTextContent('A pasta deste projeto não existe mais.');
    expect(recents).toHaveBeenCalledTimes(2);
  });

  it('depois de escolher uma pasta pelo diálogo, a lista é recarregada do host', async () => {
    const recents = vi.fn().mockResolvedValueOnce([]).mockResolvedValue([recent()]);
    renderSelection({ pick: vi.fn().mockResolvedValue(opened), recents });

    await userEvent.click(await screen.findByRole('button', { name: 'Selecionar pasta' }));

    expect(await screen.findByRole('heading', { name: 'Projetos recentes' })).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderSelection({ pick: vi.fn(), recents: vi.fn().mockResolvedValue([recent()]) });
    await screen.findByRole('heading', { name: 'Projetos recentes' });

    await expectNoA11yViolations(container);
  });
});
