/** Pede ao host para subir o motor de novo; ele só aceita quando o motor está parado. */
export async function restartEngine(): Promise<void> {
  const { invoke } = await import('@tauri-apps/api/core');
  await invoke('restart_engine');
}
