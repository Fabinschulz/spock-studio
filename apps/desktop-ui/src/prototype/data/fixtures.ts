import applyJson from '@contracts/fixtures/golden/apply-result.json';
import discoveryJson from '@contracts/fixtures/golden/discovery.json';
import planJson from '@contracts/fixtures/golden/plan.json';

import { discoverySchema, planSchema, runResultSchema } from '@/shared/schema';

/**
 * DADOS DE DEMONSTRAÇÃO. São as respostas "golden" do motor (packages/contracts), validadas pelos mesmos
 * esquemas Zod que a aplicação usa: o formato é o real, o conteúdo é de um projeto de exemplo.
 */
export const discovery = discoverySchema.parse(discoveryJson);
export const plan = planSchema.parse(planJson);
export const applyResult = runResultSchema.parse(applyJson);

export const demoProject = {
  name: 'empreganet-api',
  path: '…\\MyApps\\empreganet-api'
} as const;

export const recentProjects = [
  { id: 'r1', name: 'empreganet-api', path: '…\\MyApps\\empreganet-api', when: 'há 1 h' },
  { id: 'r2', name: 'mariliz-site', path: '…\\MyApps\\mariliz-site', when: 'anteontem' }
] as const;

/** Execuções anteriores do projeto (o motor as lista em harness.runs; aqui são de exemplo). */
export const demoRuns = [
  { id: 'run-0007', kind: 'Aplicação', outcome: 'Concluído', when: 'ontem, 16:42', files: 11 },
  { id: 'run-0006', kind: 'Validação', outcome: 'Concluído com limitações', when: 'ontem, 16:50', files: 0 }
] as const;
