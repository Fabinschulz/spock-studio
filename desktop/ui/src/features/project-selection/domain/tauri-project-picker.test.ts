import { beforeEach, describe, expect, it, vi } from 'vitest';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));

import { tauriProjectPicker } from './tauri-project-picker';
import { ProjectPickError } from './types';

describe('seletor de projeto do host', () => {
  beforeEach(() => invoke.mockReset());

  it('resposta do host fora do formato não chega ao resto da interface', async () => {
    invoke.mockResolvedValue({ displayName: 'Loja' });

    await expect(tauriProjectPicker.pick()).rejects.toBeInstanceOf(ProjectPickError);
  });

  it('diálogo cancelado volta como nulo e escolha válida passa', async () => {
    invoke.mockResolvedValueOnce(null);
    await expect(tauriProjectPicker.pick()).resolves.toBeNull();

    const project = { projectId: 'p-0123456789ab', displayName: 'Loja', displayPath: '~/loja' };
    invoke.mockResolvedValueOnce(project);
    await expect(tauriProjectPicker.pick()).resolves.toEqual(project);
  });
});
