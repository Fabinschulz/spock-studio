/** Verdadeiro quando a interface roda dentro do host Tauri (e não numa aba de navegador, como em desenvolvimento). */
export function isTauriRuntime(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window;
}
