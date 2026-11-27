export const paths = {
  home: '/',
  project: '/projeto',
  projectHarness: '/projeto/harness',
  projectKnowledge: '/projeto/conhecimento',
  projectArchitecture: '/projeto/arquitetura',
  projectCatalog: '/projeto/catalogo',
  configure: '/configurar',
  engineeringRequest: '/pedido',
  agents: '/agentes',
  security: '/seguranca',
  quality: '/qualidade',
  idea: '/ideia',
  portfolio: '/portfolio',
  management: '/gestao',
  assistant: '/gestao/assistente',
  report: '/gestao/relatorio',
  runs: '/execucoes',
  timeline: '/linha-do-tempo',
  audit: '/auditoria',
  integrations: '/integracoes',
  cloud: '/cloud',
  infrastructure: '/infraestrutura',
  // A tela dos modelos de IA abre pelos dois menus, com o mesmo nome (DCF2): o provedor de IA é uma conexão como as
  // outras e também uma preferência deste computador.
  connectionsModels: '/conexoes/modelos',
  settings: '/configuracoes',
  settingsAi: '/configuracoes/ia',
  settingsDiagnostics: '/configuracoes/diagnostico',
  settingsAbout: '/configuracoes/sobre'
} as const;

export const executionPattern = '/execucao/:runId';

export type RouteName = keyof typeof paths;

export function pathTo(name: RouteName): string {
  return paths[name];
}

/** O prefixo vale também para tudo abaixo dele: `/gestao` cobre `/gestao/assistente`. */
export function isWithin(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

export function executionPath(runId: string): string {
  return executionPattern.replace(':runId', encodeURIComponent(runId));
}
