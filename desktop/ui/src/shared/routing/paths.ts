/** Fonte única dos caminhos do aplicativo (roteador por hash, dentro do host Tauri). */
export const paths = {
  home: '/',
  configure: '/configurar',
  engineeringRequest: '/pedido',
  settings: '/configuracoes'
} as const;

/** Padrão da rota de uma execução; `executionPath` monta o caminho concreto. */
export const executionPattern = '/execucao/:runId';

export type RouteName = keyof typeof paths;

export function pathTo(name: RouteName): string {
  return paths[name];
}

export function executionPath(runId: string): string {
  return executionPattern.replace(':runId', encodeURIComponent(runId));
}
