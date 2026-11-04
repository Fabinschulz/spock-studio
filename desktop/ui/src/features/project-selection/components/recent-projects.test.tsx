import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { RecentProject } from '../domain/types';
import { RecentProjects } from './recent-projects';

const recent = (id: string, displayPath: string, available = true): RecentProject => ({
  id,
  displayName: 'api',
  displayPath,
  lastOpenedMs: Date.UTC(2026, 10, 1),
  available
});

describe('RecentProjects', () => {
  it('marca como atual só o recente com o mesmo id, mesmo com nomes repetidos', () => {
    render(
      <RecentProjects
        recents={[recent('r-1', '~/loja/api'), recent('r-2', '~/banco/api')]}
        currentId="r-2"
        onOpen={() => undefined}
      />
    );

    const [first, second] = screen.getAllByRole('button');
    expect(first).not.toHaveAttribute('aria-current');
    expect(second).toHaveAttribute('aria-current', 'true');
  });

  it('abre pelo id e não abre pasta indisponível', async () => {
    const onOpen = vi.fn();
    render(<RecentProjects recents={[recent('r-1', '~/a'), recent('r-2', '~/b', false)]} onOpen={onOpen} />);

    const [available, missing] = screen.getAllByRole('button');
    await userEvent.click(available!);
    await userEvent.click(missing!);

    expect(onOpen).toHaveBeenCalledExactlyOnceWith('r-1');
    expect(missing).toHaveAccessibleDescription(/Indisponível/);
  });
});
