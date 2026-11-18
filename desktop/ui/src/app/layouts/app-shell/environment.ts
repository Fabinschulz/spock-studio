import { executionPattern, isWithin, paths } from '@/shared/routing';

/** Uma visão, não uma permissão: o aplicativo não tem identidade de usuário, então nada aqui restringe o que se pode abrir. */
export type Environment = 'engineering' | 'management';

export interface EnvironmentEntry {
  id: Environment;
  label: string;
  to: string;
}

export const ENVIRONMENTS: readonly EnvironmentEntry[] = [
  { id: 'engineering', label: 'Engenharia', to: paths.project },
  { id: 'management', label: 'Gestão', to: paths.management }
];

export const DEFAULT_ENVIRONMENT: Environment = 'engineering';

// A execução tem rota própria (`/execucao/:runId`) e pertence à Engenharia: o prefixo sai do mesmo padrão das rotas.
const EXECUTION_PREFIX = executionPattern.split('/:')[0] ?? '';

const OWNED: Record<Environment, readonly string[]> = {
  engineering: [
    paths.project,
    paths.configure,
    paths.engineeringRequest,
    paths.agents,
    paths.security,
    paths.idea,
    paths.infrastructure,
    EXECUTION_PREFIX
  ],
  management: [paths.portfolio, paths.management]
};

/** `null` nas telas comuns (Início, Integrações, Configurações): nelas o shell mantém o ambiente em que a pessoa estava. */
export function environmentOf(pathname: string): Environment | null {
  for (const entry of ENVIRONMENTS) {
    if (OWNED[entry.id].some((prefix) => prefix !== '' && isWithin(pathname, prefix))) return entry.id;
  }
  return null;
}

export function isEnvironment(value: unknown): value is Environment {
  return ENVIRONMENTS.some((entry) => entry.id === value);
}
