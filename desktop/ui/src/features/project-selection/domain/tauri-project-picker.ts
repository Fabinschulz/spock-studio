import { z } from 'zod';
import { ProjectPickError, type ProjectPickerPort, type RecentProject, type SelectedProject } from './types';

export { isTauriRuntime } from '@/shared/api';

const recentSchema = z.object({
  id: z.string(),
  displayName: z.string(),
  displayPath: z.string(),
  lastOpenedMs: z.number(),
  available: z.boolean()
});

const selectedSchema = z.object({
  projectId: z.string(),
  displayName: z.string(),
  displayPath: z.string(),
  recentId: z.string().optional()
});

function toPickError(error: unknown, fallback: string): ProjectPickError {
  const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : 'UNKNOWN';
  const message = typeof error === 'object' && error !== null && 'message' in error ? String(error.message) : fallback;
  return new ProjectPickError(code, message);
}

/** Chama os comandos do host Tauri. O diálogo nativo abre no lado Rust e o caminho nunca passa pela UI. */
export const tauriProjectPicker: ProjectPickerPort = {
  async pick(): Promise<SelectedProject | null> {
    const { invoke } = await import('@tauri-apps/api/core');
    try {
      return selectedSchema.nullable().parse(await invoke('pick_project_folder'));
    } catch (error) {
      throw toPickError(error, 'Não foi possível selecionar a pasta.');
    }
  },

  async recents(): Promise<RecentProject[]> {
    const { invoke } = await import('@tauri-apps/api/core');
    const parsed = z.array(recentSchema).safeParse(await invoke('list_recent_projects'));
    return parsed.success ? parsed.data : [];
  },

  async openRecent(id: string): Promise<SelectedProject> {
    const { invoke } = await import('@tauri-apps/api/core');
    try {
      return selectedSchema.parse(await invoke('open_recent_project', { id }));
    } catch (error) {
      throw toPickError(error, 'Não foi possível abrir o projeto.');
    }
  }
};
