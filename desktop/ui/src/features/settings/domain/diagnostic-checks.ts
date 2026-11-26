import type { GlossaryId, Tone } from '@/shared/components';
import type { EngineReport, Resource } from '@/shared/hooks';
import { paths } from '@/shared/routing';
import type { AiTool, ModelConnection } from '@/shared/schema';

import { describeFailure, withNextStep } from './model-errors';

export type CheckState = 'ok' | 'normal' | 'attention' | 'problem' | 'waiting';

export interface DiagnosticCheck {
  id: 'engine' | 'localIntelligence' | 'model' | 'keyStorage' | 'aiTools';
  title: string;
  term?: GlossaryId;
  state: CheckState;
  /** O que o estado quer dizer e, quando há algo a fazer, o quê. */
  summary: string;
  action?: { label: string; to: string };
}

export const CHECK_STATE: Record<CheckState, { label: string; tone: Tone }> = {
  ok: { label: 'Certo', tone: 'success' },
  normal: { label: 'Normal', tone: 'info' },
  attention: { label: 'Atenção', tone: 'warning' },
  problem: { label: 'Problema', tone: 'danger' },
  waiting: { label: 'Aguardando', tone: 'neutral' }
};

export interface ChecksInput {
  engine: EngineReport & { hosted: boolean };
  connection: Resource<ModelConnection>;
  tools: Resource<AiTool[]>;
}

const NO_HOST = 'Indisponível na prévia do navegador.';
const ENGINE_DOWN = 'Depende do motor, que está parado.';
const OPEN_MODELS = { label: 'Abrir Modelos de IA', to: paths.connectionsModels };

function engineCheck({ engine }: ChecksInput): DiagnosticCheck {
  const base = { id: 'engine', title: 'Motor', term: 'engine' } as const;
  if (!engine.hosted) return { ...base, state: 'normal', summary: NO_HOST };
  switch (engine.status) {
    case 'ready':
      return { ...base, state: 'ok', summary: 'Pronto.' };
    case 'crashed':
      return { ...base, state: 'problem', summary: 'Parou e não voltou sozinho. Feche e abra o aplicativo.' };
    default:
      return { ...base, state: 'waiting', summary: 'Iniciando.' };
  }
}

function localIntelligenceCheck({ engine }: ChecksInput): DiagnosticCheck {
  const base = { id: 'localIntelligence', title: 'Inteligência local', term: 'localIntelligence' } as const;
  if (!engine.hosted) return { ...base, state: 'normal', summary: NO_HOST };
  if (engine.status === 'crashed') return { ...base, state: 'waiting', summary: ENGINE_DOWN };
  switch (engine.aiRuntime) {
    case 'ready':
      return { ...base, state: 'ok', summary: 'Pronta.' };
    case 'stopped':
      return { ...base, state: 'normal', summary: 'Parada. Ela liga no primeiro pedido que precisa dela.' };
    case 'unavailable':
      return {
        ...base,
        state: 'problem',
        summary:
          'Indisponível neste computador: a busca no conhecimento do projeto e os fluxos de engenharia não rodam. Copie as informações abaixo para o suporte.'
      };
    default:
      return { ...base, state: 'waiting', summary: 'Verificando.' };
  }
}

/** Uma leitura que falhou porque o motor parou não é um problema dela: o problema é o motor, que já aparece acima. */
function unreadable(input: ChecksInput, error: unknown, fallback: string): Pick<DiagnosticCheck, 'state' | 'summary'> {
  if (!input.engine.hosted) return { state: 'normal', summary: NO_HOST };
  if (input.engine.status === 'crashed') return { state: 'waiting', summary: ENGINE_DOWN };
  if (input.engine.status !== 'ready') return { state: 'waiting', summary: 'Aguardando o motor iniciar.' };
  return { state: 'attention', summary: describeFailure(error, fallback) };
}

function modelCheck(input: ChecksInput): DiagnosticCheck {
  const base = { id: 'model', title: 'Modelo de IA', term: 'model' } as const;
  const { connection } = input;
  if (connection.status === 'loading') return { ...base, state: 'waiting', summary: 'Lendo a configuração.' };
  if (connection.status === 'error')
    return { ...base, ...unreadable(input, connection.error, 'Não foi possível ler a configuração do modelo.') };

  const configured = connection.value.providers.filter((provider) => provider.configured);
  const main = configured.find((provider) => provider.isDefault) ?? configured[0];
  if (!main)
    return {
      ...base,
      state: 'normal',
      summary: 'Nenhum conectado. É opcional: sem ele, as leituras e verificações locais continuam funcionando.',
      action: { label: 'Conectar um modelo', to: paths.connectionsModels }
    };

  if (main.status === 'error') {
    const reason = main.lastError
      ? withNextStep(main.lastError.code, main.lastError.message)
      : 'A última conferência falhou.';
    return { ...base, state: 'problem', summary: `${main.name}, o provedor padrão: ${reason}`, action: OPEN_MODELS };
  }
  if (main.status !== 'connected')
    return {
      ...base,
      state: 'attention',
      summary: `A chave de ${main.name}, o provedor padrão, ainda não foi conferida com o provedor.`,
      action: OPEN_MODELS
    };

  const failing = configured.filter((provider) => provider.status === 'error').map((provider) => provider.name);
  if (failing.length > 0)
    return {
      ...base,
      state: 'attention',
      summary: `${main.name} conectado, com o modelo ${main.model}. Com erro: ${failing.join(', ')}.`,
      action: OPEN_MODELS
    };
  return { ...base, state: 'ok', summary: `${main.name} conectado, com o modelo ${main.model}.` };
}

function keyStorageCheck({ connection }: ChecksInput): DiagnosticCheck | null {
  if (connection.status !== 'ready') return null;
  const configured = connection.value.providers.filter((provider) => provider.configured);
  if (configured.length === 0) return null;

  const base = { id: 'keyStorage', title: 'Onde a chave fica' } as const;
  return configured.some((provider) => provider.protection === 'file')
    ? {
        ...base,
        state: 'attention',
        summary:
          'Em arquivo neste computador, sem a proteção do sistema: não há cofre disponível aqui. Conexões › Serviços diz o que falta.',
        action: { label: 'Abrir Conexões › Serviços', to: paths.integrations }
      }
    : { ...base, state: 'ok', summary: 'Guardada com a proteção do sistema operacional.' };
}

function aiToolsCheck(input: ChecksInput): DiagnosticCheck {
  const base = { id: 'aiTools', title: 'Ferramentas de IA', term: 'aiEnvironment' } as const;
  const { tools } = input;
  if (tools.status === 'loading') return { ...base, state: 'waiting', summary: 'Procurando as ferramentas.' };
  if (tools.status === 'error')
    return { ...base, ...unreadable(input, tools.error, 'Não foi possível procurar as ferramentas.') };

  const found = tools.value.filter((tool) => tool.installed).map((tool) => tool.name);
  return found.length > 0
    ? { ...base, state: 'ok', summary: `${found.length} de ${tools.value.length} encontradas: ${found.join(', ')}.` }
    : {
        ...base,
        state: 'normal',
        summary: 'Nenhuma encontrada. Elas só fazem falta para usar o Spock Studio de dentro de uma delas.'
      };
}

export function buildChecks(input: ChecksInput): DiagnosticCheck[] {
  return [
    engineCheck(input),
    localIntelligenceCheck(input),
    modelCheck(input),
    keyStorageCheck(input),
    aiToolsCheck(input)
  ].filter((check): check is DiagnosticCheck => check !== null);
}

export function formatChecks(checks: readonly DiagnosticCheck[]): string {
  return checks.map((check) => `${check.title}: ${CHECK_STATE[check.state].label}. ${check.summary}`).join('\n');
}
