import { executionPattern, paths } from '@/shared/routing';

/**
 * Os dois ambientes do produto sobre o mesmo projeto. É uma visão (o que o menu mostra e como se fala), não uma
 * permissão: o aplicativo não tem identidade de usuário, então nada aqui restringe o que alguém pode abrir.
 */
export type Environment = 'engineering' | 'management';

export interface EnvironmentEntry {
  id: Environment;
  label: string;
  /** Onde se cai ao escolher o ambiente. */
  to: string;
}

export const ENVIRONMENTS: readonly EnvironmentEntry[] = [
  { id: 'engineering', label: 'Engenharia', to: paths.project },
  { id: 'management', label: 'Gestão', to: paths.management }
];

export const DEFAULT_ENVIRONMENT: Environment = 'engineering';

// A execução tem rota própria (`/execucao/:runId`) e pertence à Engenharia: o prefixo sai do mesmo padrão das rotas.
const EXECUTION_PREFIX = executionPattern.split('/:')[0] ?? '';

/** Os caminhos de cada ambiente. Um caminho vale também para tudo abaixo dele (`/gestao` cobre `/gestao/assistente`). */
const OWNED: Record<Environment, readonly string[]> = {
  engineering: [paths.project, paths.configure, paths.engineeringRequest, EXECUTION_PREFIX],
  management: [paths.portfolio, paths.management]
};

/**
 * O ambiente a que a rota pertence, ou `null` nas telas comuns (Início, Integrações, Configurações). Nelas o shell mantém
 * o ambiente em que a pessoa estava.
 */
export function environmentOf(pathname: string): Environment | null {
  for (const entry of ENVIRONMENTS) {
    const owned = OWNED[entry.id].some(
      (prefix) => prefix !== '' && (pathname === prefix || pathname.startsWith(`${prefix}/`))
    );
    if (owned) return entry.id;
  }
  return null;
}

export function isEnvironment(value: unknown): value is Environment {
  return ENVIRONMENTS.some((entry) => entry.id === value);
}
