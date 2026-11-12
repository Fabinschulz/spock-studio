import { describe, expect, it } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import engineeringHarness from '@contracts/fixtures/engineering/harness.json';
import engineeringKnowledge from '@contracts/fixtures/engineering/knowledge.json';
import engineeringRevisions from '@contracts/fixtures/engineering/revisions.json';
import engineeringProfile from '@contracts/fixtures/engineering/profile.json';
import applyResult from '@contracts/fixtures/golden/apply-result.json';
import discovery from '@contracts/fixtures/golden/discovery.json';
import plan from '@contracts/fixtures/golden/plan.json';
import revertResult from '@contracts/fixtures/golden/revert-result.json';
import validateResult from '@contracts/fixtures/golden/validate-result.json';
import integrationCatalog from '@contracts/fixtures/integrations/catalog.json';
import integrationConnections from '@contracts/fixtures/integrations/connections.json';
import integrationHistory from '@contracts/fixtures/integrations/history.json';
import integrationList from '@contracts/fixtures/integrations/integrations.json';
import assistantAnswer from '@contracts/fixtures/management/assistant-answer.json';
import assistantEvidence from '@contracts/fixtures/management/assistant-evidence.json';
import managementForecast from '@contracts/fixtures/management/forecast.json';
import managementMetrics from '@contracts/fixtures/management/metrics.json';
import managementPortfolio from '@contracts/fixtures/management/portfolio.json';
import managementRisks from '@contracts/fixtures/management/risks.json';
import runDetail from '@contracts/fixtures/runs/run-detail.json';
import runList from '@contracts/fixtures/runs/run-list.json';
import evalCompleted from '@contracts/fixtures/valid/event.eval.completed.json';
import evalStarted from '@contracts/fixtures/valid/event.eval.started.json';
import runPlan from '@contracts/fixtures/valid/event.run.plan.json';
import stepStarted from '@contracts/fixtures/valid/event.step.started.json';
import workflowStarted from '@contracts/fixtures/valid/event.workflow.started.json';

import {
  assistantAnswerSchema,
  connectionListSchema,
  discoverySchema,
  evidencePackSchema,
  harnessRevisionsSchema,
  harnessStatusSchema,
  integrationCatalogSchema,
  integrationListSchema,
  portfolioSchema,
  projectForecastSchema,
  projectMetricsSchema,
  projectRisksSchema,
  planSchema,
  profileResultSchema,
  projectKnowledgeSchema,
  runDetailSchema,
  runEventSchema,
  runListSchema,
  runResultSchema,
  runSnapshotSchema,
  syncHistorySchema,
  workflowStartParamsSchema,
  workflowStartResultSchema
} from '.';

/** As fixtures são geradas pelo motor real (MARILIZ_UPDATE_GOLDEN=1 dotnet test). Se o formato mudar, este teste falha. */
describe('contrato do motor (fixtures golden)', () => {
  it('descoberta', () => {
    const parsed = discoverySchema.parse(discovery);
    expect(parsed.stacks[0]).toMatchObject({ id: 'dotnet', version: 'net10.0', confidence: 'high' });
    expect(parsed.specification.state).toBe('not_installed');
  });

  it('plano, com comando planejado e conflito com diff', () => {
    const parsed = planSchema.parse(plan);
    expect(parsed.commands[0]).toMatchObject({ id: 'specification-install', executable: 'npx' });
    expect(parsed.commands[0]?.effects).toContain('write_local');
    const conflict = parsed.changes.find((c) => c.action === 'conflict');
    expect(conflict?.path).toBe('.claude/agents/code-reviewer.md');
    expect(conflict?.diff).toContain('@@');
  });

  it('resultado de aplicação com Evals e os quatro estados do contrato', () => {
    const parsed = runResultSchema.parse(applyResult);
    expect(parsed.outcome).toBe('completed');
    const statuses = new Set(parsed.validation?.results.map((r) => r.status));
    expect(statuses.has('passed')).toBe(true);
    expect(statuses.has('not_run')).toBe(true);
    expect(parsed.rollbackAvailable).toBe(true);
  });

  it('resultado de validação, de reversão e de falha', () => {
    expect(runResultSchema.parse(validateResult).kind).toBe('validate');
    expect(runResultSchema.parse(revertResult)).toMatchObject({ kind: 'revert', verified: true });
    expect(runResultSchema.parse(applyFailure).error?.code).toBe('COMMAND_FAILED');
  });

  it('snapshot de execução com resultado ainda ausente', () => {
    const snapshot = {
      runId: 'run-1',
      projectId: 'p-1',
      kind: 'apply',
      state: 'running',
      lastSeq: 3,
      events: [{ seq: 0, type: 'run.state', data: { state: 'running' } }],
      result: null
    };
    expect(runSnapshotSchema.parse(snapshot).result).toBeNull();
  });

  it('workflow.start: parâmetros e resultado alinhados ao contrato', () => {
    expect(
      workflowStartParamsSchema.parse({
        projectId: 'p-000000000000',
        input: { goal: 'descrever o projeto', impact: true, requiresApproval: false }
      })
    ).toMatchObject({ projectId: 'p-000000000000' });
    expect(workflowStartResultSchema.parse({ runId: 'run-1' }).runId).toBe('run-1');
    expect(() => workflowStartParamsSchema.parse({ projectId: 'p-1', extra: true })).toThrow();
  });

  it('evento workflow.started no canal que a interface já escuta', () => {
    const parsed = runEventSchema.parse(workflowStarted);
    expect(parsed.type).toBe('workflow.started');
    expect(parsed.data.workflowId).toBe('engineering.request.v1');
    expect(parsed.data.status).toBe('started');
  });

  it('eventos de etapas e de Evals: o carimbo `at` passa pelo schema e não é descartado', () => {
    expect(runEventSchema.parse(stepStarted).at).toBe('2026-11-05T12:30:00.123Z');
    expect(runEventSchema.parse(runPlan).data.steps).toHaveLength(6);
    expect(runEventSchema.parse(evalStarted).data).toMatchObject({ id: 'EV-AGENT-001', kind: 'deterministic' });
    expect(runEventSchema.parse(evalCompleted).data).toMatchObject({ id: 'EV-AGENT-001', status: 'passed' });
    // Motor antigo: sem `at`, o evento continua válido.
    expect(runEventSchema.parse(workflowStarted).at).toBeUndefined();
  });

  it('rejeita um formato inesperado em vez de aceitar em silêncio', () => {
    expect(() => planSchema.parse({ ...plan, changes: [{ ...plan.changes[0], action: 'explode' }] })).toThrow();
    expect(() =>
      discoverySchema.parse({ ...discovery, specification: { ...discovery.specification, state: 'quase' } })
    ).toThrow();
    expect(() => runResultSchema.parse({ ...applyResult, outcome: 'otimo' })).toThrow();
  });
});

/** Geradas pelo motor real em IntegrationHubTests (MARILIZ_UPDATE_GOLDEN=1 dotnet test). */
describe('contrato da central de integrações (fixtures do motor)', () => {
  it('catálogo de conectores, com os campos que a interface desenha', () => {
    const parsed = integrationCatalogSchema.parse(integrationCatalog);
    expect(parsed.connectors.map((connector) => connector.provider)).toEqual(['github', 'jira']);
    expect(parsed.connectors[1]?.settings.map((field) => field.name)).toEqual(['site', 'email']);
  });

  it('conexões sem nenhum campo de credencial', () => {
    const parsed = connectionListSchema.parse(integrationConnections);
    expect(parsed.connections[0]).toMatchObject({ provider: 'github', status: 'connected', projects: 1 });
    expect(JSON.stringify(integrationConnections)).not.toMatch(/secret|token|password/i);
  });

  it('vínculo do projeto com recorte, política fechada e resumo do sincronizado', () => {
    const [integration] = integrationListSchema.parse(integrationList).integrations;
    expect(integration?.scope).toEqual({
      resource: 'acme/app',
      resourceName: 'acme/app',
      dataKinds: ['issues', 'pullRequests']
    });
    expect(integration?.policy.feedsKnowledge).toBe(false);
    expect(integration?.items).toMatchObject({ total: 2, byStatusCategory: { done: 1, todo: 1 } });
  });

  it('histórico de sincronizações', () => {
    expect(syncHistorySchema.parse(integrationHistory).syncs[0]).toMatchObject({ status: 'succeeded', fetched: 2 });
  });
});

describe('contrato da visão gerencial (fixtures do motor)', () => {
  it('indicadores por tipo, com motivo quando não há valor', () => {
    const parsed = projectMetricsSchema.parse(managementMetrics);
    expect(parsed.kinds.map((kind) => kind.kind)).toEqual(['issues', 'pullRequests']);
    expect(parsed.kinds[0]?.throughput.weeks).toHaveLength(parsed.periodWeeks);
    expect(parsed.kinds[0]?.overdue).toMatchObject({ value: null, unavailable: expect.any(String) });
  });

  it('portfólio com uma linha por projeto e os pontos de atenção', () => {
    const parsed = portfolioSchema.parse(managementPortfolio);
    expect(parsed.projects.map((project) => project.signals.map((signal) => signal.id))).toEqual([['no-sources'], []]);
  });
});

describe('contrato de previsão e riscos (fixtures do motor)', () => {
  it('previsão com três cenários, confiança e premissas; sem base, motivo e nenhum cenário', () => {
    const parsed = projectForecastSchema.parse(managementForecast);
    const [issues, pullRequests] = parsed.kinds;
    expect(issues?.scenarios.map((scenario) => scenario.percentile)).toEqual([15, 50, 85]);
    expect(issues?.confidence).not.toBeNull();
    expect(pullRequests).toMatchObject({ scenarios: [], confidence: null, unavailable: expect.any(String) });
    expect(parsed.assumptions.length).toBeGreaterThan(0);
  });

  it('riscos por regra, com evidências, itens e origem explícita', () => {
    const parsed = projectRisksSchema.parse(managementRisks);
    expect(parsed.risks.map((risk) => risk.id)).toEqual(['overdue', 'review-queue']);
    expect(parsed.risks.every((risk) => risk.origin === 'rule' && risk.evidence.length > 0)).toBe(true);
  });
});

describe('contrato do assistente gerencial (fixtures do motor)', () => {
  it('pacote de evidências com ids e o que ficou de fora', () => {
    const parsed = evidencePackSchema.parse(assistantEvidence);
    expect(parsed.facts.some((fact) => fact.id === 'flow.issues.throughput')).toBe(true);
    expect(parsed.withheld.join(' ')).toContain('Nenhum código');
  });

  it('resposta separada em seções, cada afirmação com fatos citados', () => {
    const parsed = assistantAnswerSchema.parse(assistantAnswer);
    const statements = [...parsed.observed, ...parsed.calculations, ...parsed.inferences, ...parsed.recommendations];
    const cited = new Set(parsed.evidence.map((fact) => fact.id));
    expect(statements.length).toBeGreaterThan(0);
    expect(statements.every((statement) => statement.evidence.every((id) => cited.has(id)))).toBe(true);
  });
});

describe('contrato do perfil tecnológico (fixture do motor)', () => {
  it('tecnologias com categoria e cobertura do Harness vindas do motor, e o histórico', () => {
    const { profile } = profileResultSchema.parse(engineeringProfile);
    expect(profile?.technologies[0]).toMatchObject({ id: 'nextjs', category: 'Framework web' });
    expect(profile?.harness[0]?.covered).toBe(true);
    expect(profile?.history[0]?.kind).toBe('created');
  });
});

describe('contrato do conhecimento do projeto (fixture do motor)', () => {
  it('arquivos com estado no índice e exclusões com motivo, sem conteúdo', () => {
    const parsed = projectKnowledgeSchema.parse(engineeringKnowledge);
    expect(parsed.indexState).toBe('read');
    expect(parsed.files.map((file) => file.state)).toEqual(['indexed', 'outdated', 'pending']);
    expect(parsed.excluded[0]?.reason).toBe('secret');
    expect(JSON.stringify(engineeringKnowledge)).not.toContain('AKIA');
  });
});

describe('contrato do estado do Harness (fixture do motor)', () => {
  it('componentes do lock com o estado conferido no disco', () => {
    const parsed = harnessStatusSchema.parse(engineeringHarness);
    expect(parsed.applied).toBe(true);
    expect(parsed.components[0]).toMatchObject({ id: 'mariliz.agent.code-reviewer', state: 'modified' });
    expect(parsed.intact + parsed.kept + parsed.modified + parsed.missing).toBe(parsed.components.length);
  });
});

describe('contrato das revisões do Harness (fixture do motor)', () => {
  it('revisões numeradas, da mais recente para a mais antiga, com a reversão ligada à aplicação', () => {
    const parsed = harnessRevisionsSchema.parse(engineeringRevisions);
    expect(parsed.revisions.map((revision) => revision.number)).toEqual([3, 2, 1]);
    expect(parsed.revisions[0]).toMatchObject({ kind: 'revert', revertsRunId: parsed.revisions[1]?.runId });
  });
});

describe('contrato da central de execuções (fixtures do motor)', () => {
  it('lista da mais recente para a mais antiga, com o projeto e o estado de cada execução', () => {
    const parsed = runListSchema.parse(runList);
    expect(parsed.runs.map((run) => run.runId)).toEqual(['run-3', 'run-2', 'run-1']);
    expect(parsed.runs[0]).toMatchObject({ kind: 'workflow', state: 'awaiting_approval', live: false });
    expect(parsed.runs[1]?.error).toBe('O runtime de IA não respondeu.');
    expect(parsed.runs[2]).toMatchObject({ canRevert: true, operations: 1 });
    expect(parsed.summary).toEqual({ total: 3, running: 0, awaitingApproval: 1 });
    expect(parsed.nextCursor).toBeNull();
  });

  it('detalhe com operações, passos e verificações, sem conteúdo gravável', () => {
    const parsed = runDetailSchema.parse(runDetail);
    expect(parsed.operations[0]).toMatchObject({ path: 'CLAUDE.md', status: 'applied' });
    expect(parsed.steps[0]).toMatchObject({ title: 'Aplicar', status: 'completed' });
    expect(parsed.evals[0]).toMatchObject({ id: 'e1', required: true });
    expect(parsed.eventCount).toBe(1);
    expect(JSON.stringify(runDetail)).not.toContain('"content"');
  });
});
