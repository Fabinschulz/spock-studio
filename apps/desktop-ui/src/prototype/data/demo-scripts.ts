import { applyResult } from './fixtures';

/**
 * REPRODUÇÃO DE DEMONSTRAÇÃO. Cada script é uma sequência gravada de eventos, com o tempo de cada um (`t`, em ms).
 * Na aplicação real não há script: os mesmos tipos de evento chegam do motor (RunEvent) e o mesmo modelo os consome.
 *
 * - `engine`   : vocabulário que o motor JÁ publica hoje (run.state, step.started, step.completed, file.applied).
 * - `proposed` : eventos que o motor AINDA NÃO publica (contrato mínimo proposto em docs/redesign). Só existem
 *                aqui para avaliar o desenho; a tela os marca como demonstração.
 */
export type EventSource = 'engine' | 'proposed';

export interface ScriptedEvent {
  t: number;
  type: string;
  source: EventSource;
  data: Record<string, unknown>;
}

const ev = (t: number, type: string, data: Record<string, unknown>, source: EventSource = 'engine'): ScriptedEvent => ({
  t,
  type,
  source,
  data
});

const created = applyResult.files?.created ?? [];
const evals = applyResult.validation?.results ?? [];

/** Etapas que a aplicação executa nesta ordem (ver ApplyExecution.cs). Os títulos são os que o motor publica. */
export const EXPECTED_APPLY_STEPS = [
  { id: 'precheck', title: 'Verificar pré-condições' },
  { id: 'backup', title: 'Guardar cópia do que será alterado' },
  { id: 'write', title: 'Aplicar as alterações aprovadas' },
  { id: 'lock', title: 'Registrar os arquivos gerenciados' },
  { id: 'validate', title: 'Validar a configuração aplicada' }
] as const;

function applyCore(): ScriptedEvent[] {
  const out: ScriptedEvent[] = [
    ev(0, 'run.state', { state: 'running' }),
    ev(400, 'step.started', { id: 'precheck', title: 'Verificar pré-condições' }),
    ev(1500, 'step.completed', { id: 'precheck', status: 'completed', detail: null }),
    ev(1700, 'step.started', { id: 'backup', title: 'Guardar cópia do que será alterado' }),
    ev(2800, 'step.completed', { id: 'backup', status: 'completed', detail: null }),
    ev(3000, 'step.started', { id: 'write', title: 'Aplicar as alterações aprovadas' })
  ];
  let t = 3000;
  for (const path of created) {
    t += 320;
    out.push(ev(t, 'file.applied', { path, op: 'create' }));
  }
  t += 300;
  out.push(ev(t, 'step.completed', { id: 'write', status: 'completed', detail: `${created.length} operação(ões)` }));
  out.push(ev(t + 200, 'step.started', { id: 'lock', title: 'Registrar os arquivos gerenciados' }));
  out.push(ev(t + 1000, 'step.completed', { id: 'lock', status: 'completed', detail: null }));
  out.push(ev(t + 1200, 'run.state', { state: 'validating' }));
  out.push(ev(t + 1250, 'step.started', { id: 'validate', title: 'Validar a configuração aplicada' }));
  return out;
}

const core = applyCore();
const validateStart = core[core.length - 1]?.t ?? 0;

/** O que o motor publica hoje: a validação é UMA etapa; os 14 Evals não emitem eventos individuais. */
export const applyScriptCurrent: ScriptedEvent[] = [
  ...core,
  ev(validateStart + 5200, 'step.completed', { id: 'validate', status: 'completed', detail: 'Passed' }),
  ev(validateStart + 5400, 'run.completed', { state: 'completed' })
];

/** Contrato proposto: o início declara as etapas e cada Eval publica seu início e seu fim. */
export const applyScriptProposed: ScriptedEvent[] = [
  ev(0, 'run.plan', { steps: EXPECTED_APPLY_STEPS }, 'proposed'),
  ...core,
  ...evals.flatMap((e, i): ScriptedEvent[] => {
    const start = validateStart + 200 + i * 340;
    return [
      ev(start, 'eval.started', { id: e.id, name: e.name, kind: e.kind }, 'proposed'),
      ev(start + 280, 'eval.completed', { id: e.id, status: e.status }, 'proposed')
    ];
  }),
  ev(validateStart + 200 + evals.length * 340 + 200, 'step.completed', {
    id: 'validate',
    status: 'completed',
    detail: 'Passed'
  }),
  ev(validateStart + 200 + evals.length * 340 + 400, 'run.completed', { state: 'completed' })
];

/**
 * Planejamento assistido. HOJE o Harness Engineer roda no ambiente de IA da pessoa, fora do Mariliz, e o motor só
 * lê .mariliz/proposal.json. Estes eventos descrevem o que a interface mostraria SE um adaptador do motor
 * orquestrasse os agentes e publicasse sua atividade. Hipótese de produto, não funcionalidade existente.
 * Os agentes são os que o catálogo realmente instala (harness-engineer, verifier); as frases de atividade são ilustrativas.
 */
export const planningScriptProposed: ScriptedEvent[] = [
  ev(
    0,
    'agent.state',
    { agent: 'harness-engineer', name: 'Harness Engineer', state: 'working', activity: 'Lendo a estrutura do projeto' },
    'proposed'
  ),
  ev(
    1800,
    'agent.state',
    {
      agent: 'harness-engineer',
      name: 'Harness Engineer',
      state: 'working',
      activity: 'Avaliando dependências e regras existentes'
    },
    'proposed'
  ),
  ev(
    3600,
    'agent.state',
    {
      agent: 'harness-engineer',
      name: 'Harness Engineer',
      state: 'working',
      activity: 'Escrevendo .mariliz/proposal.json'
    },
    'proposed'
  ),
  ev(
    4800,
    'agent.state',
    { agent: 'harness-engineer', name: 'Harness Engineer', state: 'done', activity: 'Proposta escrita' },
    'proposed'
  ),
  ev(
    4850,
    'agent.state',
    {
      agent: 'verifier',
      name: 'Verifier',
      state: 'working',
      activity: 'Conferindo a proposta contra os critérios de aceite'
    },
    'proposed'
  ),
  ev(
    6800,
    'agent.state',
    {
      agent: 'verifier',
      name: 'Verifier',
      state: 'done',
      activity: 'Sem conflitos encontrados'
    },
    'proposed'
  )
];

export const scriptDuration = (script: ScriptedEvent[]) => script[script.length - 1]?.t ?? 0;
