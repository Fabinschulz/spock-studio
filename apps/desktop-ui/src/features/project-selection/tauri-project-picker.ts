import { ProjectPickError, type ProjectPickerPort, type SelectedProject } from './project-picker-port';

export function isTauriRuntime(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;
}

/** Chama o comando `pick_project_folder` do host Tauri. O diálogo nativo abre no lado Rust. */
export const tauriProjectPicker: ProjectPickerPort = {
  async pick(): Promise<SelectedProject | null> {
    const { invoke } = await import('@tauri-apps/api/core');
    try {
      return await invoke<SelectedProject | null>('pick_project_folder');
    } catch (error) {
      const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : 'UNKNOWN';
      const message =
        typeof error === 'object' && error !== null && 'message' in error
          ? String(error.message)
          : 'Não foi possível selecionar a pasta.';
      throw new ProjectPickError(code, message);
    }
  }
};
