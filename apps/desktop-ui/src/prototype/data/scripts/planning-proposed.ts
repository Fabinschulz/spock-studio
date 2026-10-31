import { createScriptedEvent } from '@/prototype/domain/create-scripted-event';
import type { ScriptedEvent } from '@/prototype/types/event';

// Hipótese de produto: hoje o Harness Engineer roda fora do Mariliz e o motor só lê .mariliz/proposal.json.
const agentEvent = (t: number, agent: string, name: string, state: string, activity: string): ScriptedEvent =>
  createScriptedEvent(t, 'agent.state', { agent, name, state, activity }, 'proposed');

const harness = (t: number, state: string, activity: string) =>
  agentEvent(t, 'harness-engineer', 'Harness Engineer', state, activity);
const verifier = (t: number, state: string, activity: string) => agentEvent(t, 'verifier', 'Verifier', state, activity);

export const planningScriptProposed: ScriptedEvent[] = [
  harness(0, 'working', 'Lendo a estrutura do projeto'),
  harness(1800, 'working', 'Avaliando dependências e regras existentes'),
  harness(3600, 'working', 'Escrevendo .mariliz/proposal.json'),
  harness(4800, 'done', 'Proposta escrita'),
  verifier(4850, 'working', 'Conferindo a proposta contra os critérios de aceite'),
  verifier(6800, 'done', 'Sem conflitos encontrados')
];
