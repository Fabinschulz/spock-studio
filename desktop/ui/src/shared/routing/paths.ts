/** Fonte única dos caminhos do aplicativo (roteador por hash, dentro do host Tauri). */
export const paths = {
  home: '/',
  configure: '/configurar',
  settings: '/configuracoes'
} as const;

export type RouteName = keyof typeof paths;

export function pathTo(name: RouteName): string {
  return paths[name];
}

export function executionPath(runId: string): string {
  return `/execucao/${encodeURIComponent(runId)}`;
}
