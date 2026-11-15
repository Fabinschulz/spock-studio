import agentResultFixture from '@contracts/fixtures/agents/run-result.json';
import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import auditFixture from '@contracts/fixtures/audit/audit.json';
import cloudCatalogFixture from '@contracts/fixtures/cloud/catalog.json';
import cloudConnectionsFixture from '@contracts/fixtures/cloud/connections.json';
import cloudLinksFixture from '@contracts/fixtures/cloud/links.json';
import cloudReadFixture from '@contracts/fixtures/cloud/read.json';
import iacLayoutFixture from '@contracts/fixtures/iac/layout.json';
import iacOfferFixture from '@contracts/fixtures/iac/offer.json';
import iacArchitectDraftFixture from '@contracts/fixtures/iac/architect-draft.json';
import iacBootstrapFixture from '@contracts/fixtures/iac/bootstrap-plan.json';
import iacGeneratePlanFixture from '@contracts/fixtures/iac/generate-plan.json';
import iacOperationsFixture from '@contracts/fixtures/iac/operations.json';
import iacStateResourcesFixture from '@contracts/fixtures/iac/state-resources.json';
import iacStateFixture from '@contracts/fixtures/iac/state.json';
import iacStatusFixture from '@contracts/fixtures/iac/status.json';
import contextReviewFixture from '@contracts/fixtures/context/review.json';
import ideConnectionFixture from '@contracts/fixtures/ide/connection.json';
import ideaDraftFixture from '@contracts/fixtures/idea/draft.json';
import ideaPlanFixture from '@contracts/fixtures/idea/plan.json';
import investigationFixture from '@contracts/fixtures/management/investigation.json';
import portfolioAnswerFixture from '@contracts/fixtures/management/portfolio-answer.json';
import portfolioEvidenceFixture from '@contracts/fixtures/management/portfolio-evidence.json';
import securityPlanFixture from '@contracts/fixtures/security/plan.json';
import securityScanFixture from '@contracts/fixtures/security/scan.json';
import timelineFixture from '@contracts/fixtures/timeline/timeline.json';
import { describe, expect, it } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import engineeringCatalog from '@contracts/fixtures/engineering/catalog.json';
import engineeringHarness from '@contracts/fixtures/engineering/harness.json';
import engineeringKnowledgeExternal from '@contracts/fixtures/engineering/knowledge-external.json';
import engineeringMcp from '@contracts/fixtures/engineering/mcp.json';
import engineeringPolicy from '@contracts/fixtures/engineering/policy.json';
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
import integrationReach from '@contracts/fixtures/integrations/reach.json';
import modelConnectionError from '@contracts/fixtures/models/connection-error.json';
import modelConnectionMulti from '@contracts/fixtures/models/connection-multi.json';
import modelConnectionPurposes from '@contracts/fixtures/models/connection-purposes.json';
import modelConnection from '@contracts/fixtures/models/connection.json';
import assistantAnswer from '@contracts/fixtures/management/assistant-answer.json';
import assistantEvidence from '@contracts/fixtures/management/assistant-evidence.json';
import managementForecast from '@contracts/fixtures/management/forecast.json';
import managementMetrics from '@contracts/fixtures/management/metrics.json';
import managementPortfolio from '@contracts/fixtures/management/portfolio.json';
import decisionGet from '@contracts/fixtures/management/decision-get.json';
import decisionList from '@contracts/fixtures/management/decision-list.json';
import decisionRecord from '@contracts/fixtures/management/decision-record.json';
import managementMilestones from '@contracts/fixtures/management/milestones.json';
import managementReport from '@contracts/fixtures/management/report.json';
import managementRiskHistory from '@contracts/fixtures/management/risk-history.json';
import managementRisks from '@contracts/fixtures/management/risks.json';
import runDetail from '@contracts/fixtures/runs/run-detail.json';
import runLink from '@contracts/fixtures/runs/run-link.json';
import runLinked from '@contracts/fixtures/runs/run-linked.json';
import runList from '@contracts/fixtures/runs/run-list.json';
import evalCompleted from '@contracts/fixtures/valid/event.eval.completed.json';
import evalStarted from '@contracts/fixtures/valid/event.eval.started.json';
import runPlan from '@contracts/fixtures/valid/event.run.plan.json';
import stepStarted from '@contracts/fixtures/valid/event.step.started.json';
import workflowStarted from '@contracts/fixtures/valid/event.workflow.started.json';

import {
  cloudCatalogSchema,
  cloudConnectionListSchema,
  cloudLinkListSchema,
  cloudReadResultSchema,
  iacArchitectDraftSchema,
  iacBootstrapPlanSchema,
  iacGeneratePlanSchema,
  iacLayoutSchema,
  iacStateReadSchema,
  iacStateReportSchema,
  iacOperationListSchema,
  iacRuntimeOfferResultSchema,
  iacRuntimeStatusResultSchema,
  assistantAnswerSchema,
  connectionListSchema,
  connectionReachListSchema,
  discoverySchema,
  evidencePackSchema,
  harnessCatalogSchema,
  modelConnectionSchema,
  mcpInventorySchema,
  agentPolicySchema,
  harnessRevisionsSchema,
  harnessStatusSchema,
  integrationCatalogSchema,
  integrationListSchema,
  portfolioSchema,
  projectForecastSchema,
  decisionGetSchema,
  decisionListSchema,
  decisionRecordResultSchema,
  projectMilestonesSchema,
  projectReportSchema,
  projectRiskHistorySchema,
  projectMetricsSchema,
  projectRisksSchema,
  planSchema,
  profileResultSchema,
  aiToolListSchema,
  agentRunResultSchema,
  ideConnectionSchema,
  contextReviewSchema,
  securityPlanSchema,
  securityReportSchema,
  ideaDraftSchema,
  ideaPlanSchema,
  ideaSpecificationView,
  projectKnowledgeSchema,
  portfolioAnswerSchema,
  portfolioEvidenceSchema,
  projectAuditTrailSchema,
  projectInvestigationSchema,
  projectTimelineSchema,
  runDetailSchema,
  linkedItemsResultSchema,
  runEventSchema,
  runLinksResultSchema,
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
    expect(parsed.risks.map((risk) => risk.id)).toEqual(['overdue', 'blocked', 'review-queue']);
    expect(parsed.risks[1]).toMatchObject({ severity: 'low', items: [{ externalId: 'A-2' }] });
    expect(parsed.rulesEvaluated).toContain('blocked');
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
    expect(parsed.summary.done).toMatchObject({ created: 1, stepsCompleted: 1, stepsTotal: 1 });
    expect(parsed.summary.validated).toMatchObject({ total: 1, passed: 1, requiredNotPassed: 0 });
    expect(JSON.stringify(runDetail)).not.toContain('"content"');
    expect(parsed.links).toEqual([]);
  });

  it('caminho inverso: o item com as execuções ligadas a ele, da mais recente para a mais antiga', () => {
    const parsed = linkedItemsResultSchema.parse(runLinked);
    expect(parsed.items[0]).toMatchObject({ externalId: '41', title: 'Pagamento por Pix', available: true });
    expect(parsed.items[0]?.runs.map((run) => run.runId)).toEqual(['run-2', 'run-1']);
    expect(parsed.items[0]?.runs[0]).toMatchObject({ kind: 'workflow', state: 'failed' });
  });

  it('ligações com o item: título e situação do dado sincronizado, e o tipo do item', () => {
    const parsed = runLinksResultSchema.parse(runLink);
    expect(parsed.links[0]).toMatchObject({
      provider: 'github',
      kind: 'issues',
      externalId: '41',
      available: true,
      title: 'Pagamento por Pix',
      statusCategory: 'inProgress'
    });
  });
});

describe('contrato dos marcos (fixture do motor)', () => {
  it('andamento, prazo, situação e previsão de cada marco, com o que está sem marco', () => {
    const parsed = projectMilestonesSchema.parse(managementMilestones);
    const v1 = parsed.milestones.find((milestone) => milestone.name === 'v1.0');
    expect(v1).toMatchObject({
      kind: 'issues',
      dueDate: '2026-12-12',
      items: 10,
      delivered: 4,
      open: 6,
      outlook: 'on_track'
    });
    expect(v1?.forecast.scenarios.map((scenario) => scenario.by)).toEqual(['2026-12-03', '2026-12-03', '2026-12-03']);
    expect(parsed.milestones.find((milestone) => milestone.name === 'v2.0')).toMatchObject({
      dueDate: null,
      outlook: 'no_due_date'
    });
    expect(parsed.unassigned).toEqual([{ kind: 'issues', items: 21, open: 1 }]);
    expect(parsed.assumptions.length).toBeGreaterThan(0);
  });
});

describe('contrato do histórico de riscos (fixture do motor)', () => {
  it('a trajetória de cada risco, do que está ativo ao já resolvido', () => {
    const parsed = projectRiskHistorySchema.parse(managementRiskHistory);
    expect(parsed.snapshots).toBe(3);
    expect(parsed.trails.map((trail) => [trail.id, trail.active])).toEqual([
      ['blocked', true],
      ['stalled-wip', true],
      ['overdue', false],
      ['scope-growth:issues', false]
    ]);
    const blocked = parsed.trails.find((trail) => trail.id === 'blocked');
    expect(blocked?.changes.map((change) => change.kind)).toEqual(['observed', 'raised', 'lowered']);
    expect(parsed.basis).toContain('antes do primeiro registro');
  });
});

describe('contrato das decisões gerenciais (fixtures do motor)', () => {
  it('o registro devolve a decisão completa com os fatos do dia e se houve redação', () => {
    const parsed = decisionRecordResultSchema.parse(decisionRecord);
    expect(parsed.redacted).toBe(false);
    expect(parsed.decision).toMatchObject({ title: 'Adiar a release', rationale: 'Três itens bloqueados.' });
    expect(parsed.decision.facts.map((fact) => fact.id)).toEqual(['flow.issues.items', 'risk.overdue']);
    expect(parsed.decision.runs.map((run) => run.runId)).toEqual(['run-1', 'run-2']);
  });

  it('a lista traz a contagem de fatos e não os fatos', () => {
    const parsed = decisionListSchema.parse(decisionList);
    expect(parsed.decisions[0]).toMatchObject({ factCount: 2 });
    expect(JSON.stringify(decisionList)).not.toContain('"facts"');
  });

  it('abrir uma decisão devolve os fatos e o que ficou de fora', () => {
    const parsed = decisionGetSchema.parse(decisionGet);
    expect(parsed.decision.facts).toHaveLength(2);
    expect(parsed.decision.withheld).toEqual(['Nenhum código é enviado.']);
  });
});

describe('contrato do relatório do período (fixture do motor)', () => {
  it('junta indicadores, previsão, riscos, marcos, decisões e execuções do mesmo período', () => {
    const parsed = projectReportSchema.parse(managementReport);
    expect(parsed.weeks).toBe(4);
    expect(parsed.metrics.periodWeeks).toBe(4);
    expect(parsed.decisions.map((decision) => decision.title)).toEqual(['Adiar a release']);
    expect(parsed.runs.map((run) => `${run.kind}/${run.state}=${run.count}`)).toEqual([
      'apply/completed=1',
      'workflow/failed=1'
    ]);
    expect(parsed.basis).toContain('nenhum texto foi escrito por modelo de linguagem');
  });
});

describe('contrato do catálogo do Harness (fixture do motor)', () => {
  it('uma ficha por componente, com o estado de cada um no projeto', () => {
    const parsed = harnessCatalogSchema.parse(engineeringCatalog);
    expect(parsed.applied).toBe(true);
    expect(parsed.entries).toHaveLength(12);
    const byId = new Map(parsed.entries.map((entry) => [entry.sheet.id, entry]));
    expect(byId.get('mariliz.agent.verifier')).toMatchObject({ presence: 'excluded', state: 'intact' });
    expect(byId.get('mariliz.rule.stack-javascript')).toMatchObject({ presence: 'not_applied', state: null });
    expect(byId.get('mariliz.rule.architecture')).toMatchObject({ presence: 'not_applied', state: null });
    expect(byId.get('mariliz.agent.code-reviewer')?.sheet.tools.map((tool) => tool.name)).toEqual([
      'Read',
      'Grep',
      'Glob',
      'Bash'
    ]);
  });

  it('nenhuma ficha tem campo vazio', () => {
    const parsed = harnessCatalogSchema.parse(engineeringCatalog);
    for (const { sheet } of parsed.entries) {
      expect(sheet.whatItDoes).not.toBe('');
      expect(sheet.whenItActs).not.toBe('');
      expect(sheet.reads.length).toBeGreaterThan(0);
      expect(sheet.canChange.length).toBeGreaterThan(0);
      expect(sheet.files.length).toBeGreaterThan(0);
    }
  });
});

describe('contrato do inventário de MCP (fixture do motor)', () => {
  it('cada servidor traz o transporte, o que roda e as variáveis, com o estado da credencial', () => {
    const parsed = mcpInventorySchema.parse(engineeringMcp);
    expect(parsed).toMatchObject({ present: true, readable: true });
    const github = parsed.servers.find((server) => server.name === 'github');
    expect(github).toMatchObject({ transport: 'stdio', runs: 'npx' });
    expect(github?.literalSecrets.map((secret) => secret.where)).toEqual(['env.GITHUB_TOKEN']);
    expect(parsed.servers.find((server) => server.name === 'docs')).toMatchObject({
      transport: 'http',
      runs: 'mcp.exemplo.com',
      literalSecrets: []
    });
  });

  it('a resposta não carrega valor de credencial', () => {
    expect(JSON.stringify(engineeringMcp)).not.toMatch(/ghp_|Bearer /);
  });
});

describe('contrato da política dos agentes (fixture do motor)', () => {
  it('traz o hook, o que ele vigia, as regras com a decisão e os limites', () => {
    const parsed = agentPolicySchema.parse(engineeringPolicy);
    expect(parsed.script).toBe('.claude/hooks/mariliz-policy.py');
    expect(parsed.watches).toContain('rodar comandos no terminal');
    expect(parsed.limits.length).toBeGreaterThan(0);
    const count = (decision: string) => parsed.rules.filter((rule) => rule.decision === decision).length;
    expect([count('deny'), count('ask'), count('allow')]).toEqual([10, 5, 2]);
    expect(parsed.rules.every((rule) => ['command', 'write'].includes(rule.scope))).toBe(true);
  });

  it('nenhuma regra tem texto vazio', () => {
    expect(agentPolicySchema.parse(engineeringPolicy).rules.every((rule) => rule.plain.length > 0)).toBe(true);
  });
});

describe('contrato do cofre no catálogo de integrações (fixture do motor)', () => {
  it('o catálogo diz se há cofre, como ele se chama e, sem cofre, o que falta', () => {
    const parsed = integrationCatalogSchema.parse(integrationCatalog);
    expect(parsed.vaultAvailable).toBe(true);
    expect(parsed.vaultName.length).toBeGreaterThan(0);
    expect(parsed.vaultHint).toBeNull();
    expect(
      integrationCatalogSchema.parse({
        ...integrationCatalog,
        vaultAvailable: false,
        vaultHint: 'Instale o libsecret-tools.'
      }).vaultHint
    ).toBe('Instale o libsecret-tools.');
  });
});

describe('contrato do resumo por conexão (fixture do motor)', () => {
  it('cada conexão traz o impacto e os projetos que a usam, com o recorte e a política', () => {
    const parsed = connectionReachListSchema.parse(integrationReach);
    expect(parsed.connections.map((entry) => entry.impact)).toEqual(['read', 'read']);
    const used = parsed.connections[0]!;
    expect(used.projects).toHaveLength(1);
    expect(used.projects[0]).toMatchObject({ resource: 'acme/app', feedsKnowledge: false });
    expect(used.projects[0]!.dataKinds.map((kind) => kind.label)).toEqual(['Issues', 'Pull requests']);
    expect(parsed.connections[1]!.projects).toEqual([]);
  });

  it('todo conector do catálogo declara o impacto e a fixture não carrega credencial nem caminho', () => {
    expect(integrationCatalogSchema.parse(integrationCatalog).connectors.map((connector) => connector.impact)).toEqual([
      'read',
      'read'
    ]);
    expect(JSON.stringify(integrationReach)).not.toMatch(/ghp_|[A-Z]:\\|\/Users\//);
  });
});

describe('contrato da conexão do modelo (fixtures do motor)', () => {
  const providerOf = (parsed: ReturnType<typeof modelConnectionSchema.parse>, id: string) =>
    parsed.providers.find((provider) => provider.id === id)!;

  it('o estado conectado diz o provedor padrão, o modelo, onde a chave está e quando foi conferida', () => {
    const parsed = modelConnectionSchema.parse(modelConnection);
    const openai = providerOf(parsed, 'openai');
    expect(parsed).toMatchObject({ configured: true, provider: 'openai' });
    expect(openai).toMatchObject({
      name: 'OpenAI',
      configured: true,
      isDefault: true,
      status: 'connected',
      protection: 'file',
      lastError: null
    });
    expect(openai.model).not.toBe('');
    expect(openai.checkedAt).not.toBeNull();
    expect(parsed.providers.map((provider) => provider.id)).toEqual(['openai', 'anthropic', 'google']);
    expect(parsed.providers.every((provider) => provider.model.length > 0)).toBe(true);
  });

  it('um provedor sem chave diz "none" e não traz proteção nem conferência', () => {
    const parsed = modelConnectionSchema.parse(modelConnection);
    const without = parsed.providers.filter((provider) => !provider.configured);
    expect(without.map((provider) => provider.id)).toEqual(['anthropic', 'google']);
    expect(without.every((provider) => provider.status === 'none' && provider.protection === null)).toBe(true);
    expect(without.every((provider) => !provider.isDefault && provider.checkedAt === null)).toBe(true);
  });

  it('o estado com erro traz o código e o motivo da recusa, no provedor que recusou', () => {
    const parsed = modelConnectionSchema.parse(modelConnectionError);
    const refused = providerOf(parsed, parsed.provider);
    expect(refused.status).toBe('error');
    expect(refused.lastError).toMatchObject({ code: 'MODEL_AUTH_FAILED' });
    expect(refused.lastError?.message).not.toBe('');
  });

  it('cada finalidade diz o provedor e o modelo de hoje, e sem escolha é o padrão do provedor padrão', () => {
    const parsed = modelConnectionSchema.parse(modelConnection);
    const fallback = providerOf(parsed, parsed.provider);
    expect(parsed.purposes.map((purpose) => purpose.id)).toEqual(['assistant', 'judge', 'workflow']);
    expect(
      parsed.purposes.every(
        (purpose) => purpose.isDefault && purpose.model === fallback.model && purpose.provider === fallback.id
      )
    ).toBe(true);
    expect(parsed.purposes.every((purpose) => purpose.label.length > 0 && purpose.description.length > 0)).toBe(true);
    expect(fallback.models.map((option) => option.id)).toContain(fallback.model);
  });

  it('uma escolha aparece só na finalidade escolhida e é um modelo que o provedor dela oferece', () => {
    const parsed = modelConnectionSchema.parse(modelConnectionPurposes);
    const judge = parsed.purposes.find((purpose) => purpose.id === 'judge')!;
    expect(judge).toMatchObject({ model: 'claude-opus-5-5', provider: 'anthropic', isDefault: false });
    expect(parsed.purposes.filter((purpose) => purpose.isDefault).map((purpose) => purpose.id)).toEqual([
      'assistant',
      'workflow'
    ]);
    expect(providerOf(parsed, judge.provider).models.map((option) => option.id)).toContain(judge.model);
  });

  it('com vários provedores, cada um tem o seu estado e as finalidades podem ser de provedores diferentes', () => {
    const parsed = modelConnectionSchema.parse(modelConnectionMulti);
    expect(parsed.providers.filter((provider) => provider.configured).map((provider) => provider.id)).toEqual([
      'openai',
      'anthropic'
    ]);
    expect(providerOf(parsed, 'openai')).toMatchObject({ isDefault: true, status: 'connected' });
    expect(providerOf(parsed, 'anthropic')).toMatchObject({ isDefault: false, status: 'error' });
    expect(providerOf(parsed, 'anthropic').lastError).toMatchObject({ code: 'MODEL_AUTH_FAILED' });
    expect(new Set(parsed.purposes.map((purpose) => purpose.provider))).toEqual(new Set(['openai', 'anthropic']));
  });

  it('nenhuma resposta carrega a chave nem endereço', () => {
    const all = [modelConnection, modelConnectionError, modelConnectionPurposes, modelConnectionMulti];
    expect(JSON.stringify(all)).not.toMatch(/sk-/);
    expect(JSON.stringify(all)).not.toMatch(/https:/);
  });
});
describe('contrato das fontes externas do conhecimento (fixture do motor)', () => {
  it('cada fonte diz se está autorizada e quantos itens dela entram no índice', () => {
    const { external } = {
      external: projectKnowledgeSchema.shape.external.parse(engineeringKnowledgeExternal.external)
    };
    expect(external.map((source) => source.authorized)).toEqual([true, false]);
    expect(external[0]).toMatchObject({
      providerName: 'GitHub',
      resourceName: 'acme/app',
      items: 1,
      documents: 1,
      withheld: 0
    });
    expect(external[1]).toMatchObject({ items: 1, documents: 0 });
    expect(new Set(external.map((source) => source.integrationId)).size).toBe(2);
  });

  it('uma fonte não autorizada nunca tem documento no índice', () => {
    const { external } = {
      external: projectKnowledgeSchema.shape.external.parse(engineeringKnowledgeExternal.external)
    };
    expect(
      external.filter((source) => !source.authorized).every((source) => source.documents === 0 && source.indexed === 0)
    ).toBe(true);
  });

  it('o conhecimento sem fonte externa traz a lista vazia', () => {
    expect(projectKnowledgeSchema.parse(engineeringKnowledge).external).toEqual([]);
  });
});

describe('contrato da linha do tempo do projeto (fixture do motor)', () => {
  it('traz um acontecimento de cada fonte, do mais recente ao mais antigo, e as fontes com a contagem', () => {
    const parsed = projectTimelineSchema.parse(timelineFixture);
    expect([...new Set(parsed.events.map((event) => event.kind))].sort()).toEqual([
      'decision',
      'harness',
      'profile',
      'risk',
      'run',
      'sync'
    ]);
    expect(parsed.events.map((event) => event.at)).toEqual(
      [...parsed.events.map((event) => event.at)].sort().reverse()
    );
    expect(parsed.sources.map((source) => [source.kind, source.count])).toEqual(
      expect.arrayContaining([
        ['risk', 3],
        ['profile', 3],
        ['harness', 1]
      ])
    );
    expect(parsed.sources.every((source) => source.available)).toBe(true);
    expect(parsed.nextBefore).toBeNull();
  });

  it('cada acontecimento tem id único e só execução e decisão têm destino', () => {
    const parsed = projectTimelineSchema.parse(timelineFixture);
    expect(new Set(parsed.events.map((event) => event.id)).size).toBe(parsed.events.length);
    for (const event of parsed.events) {
      expect(['ok', 'attention', 'problem', 'info']).toContain(event.level);
      expect(event.refType === null).toBe(event.refId === null);
      if (event.refType !== null) expect(['run', 'decision']).toContain(event.refType);
    }
  });

  it('o texto não carrega identificador de item nem credencial', () => {
    expect(JSON.stringify(timelineFixture)).not.toMatch(/acme\/app#|ghp_/);
  });
});

describe('contrato do resultado de um agente (fixture do motor)', () => {
  it('traz a ferramenta, o código de saída e a resposta', () => {
    expect(agentRunResultSchema.parse(agentResultFixture)).toEqual({
      kind: 'agent',
      tool: 'claude-code',
      toolName: 'Claude Code',
      exitCode: 0,
      error: null,
      answer: 'Resposta do agente.'
    });
  });
});

describe('contrato da ideia ao projeto (fixtures do motor)', () => {
  it('a etapa traz o que o modelo escreveu e cada verificação do motor', () => {
    const parsed = ideaDraftSchema.parse(ideaDraftFixture);

    expect(parsed).toMatchObject({ stage: 'specification', valid: true, attempts: 1 });
    expect(parsed.checks.map((check) => check.id)).toEqual(['shape', 'ids', 'shall', 'priority', 'secrets']);
    expect(ideaSpecificationView.parse(parsed.artifact).requirements.map((item) => item.id)).toEqual([
      'REQ-01',
      'REQ-02',
      'REQ-03'
    ]);
  });

  it('o plano grava só os três documentos em .mariliz/arquitetura', () => {
    expect(ideaPlanSchema.parse(ideaPlanFixture).changes.map((change) => change.path)).toEqual([
      '.mariliz/arquitetura/especificacao.md',
      '.mariliz/arquitetura/arquitetura.md',
      '.mariliz/arquitetura/backlog.md'
    ]);
  });
});

describe('contrato da segurança (fixtures do motor)', () => {
  it('a varredura traz cada tipo de achado e nunca o valor encontrado', () => {
    const parsed = securityReportSchema.parse(securityScanFixture);

    expect(new Set(parsed.findings.map((finding) => finding.kind))).toEqual(
      new Set(['secret', 'unprotected-file', 'ai-permission'])
    );
    expect(parsed.findings.find((finding) => finding.kind === 'secret')).toMatchObject({
      path: 'src/config.ts',
      line: 2
    });
    expect(JSON.stringify(securityScanFixture)).not.toMatch(/ghp_/);
  });

  it('o plano traz a diferença de cada arquivo e os achados que corrige', () => {
    const parsed = securityPlanSchema.parse(securityPlanFixture);
    const ids = new Set(securityReportSchema.parse(securityScanFixture).findings.map((finding) => finding.id));

    expect(parsed.changes.map((change) => change.path)).toEqual(['.gitignore', '.claude/settings.json']);
    expect(parsed.findingIds.every((id) => ids.has(id))).toBe(true);
    expect(parsed.findingIds.length + parsed.manual).toBe(ids.size);
  });
});

describe('contrato da revisão de contexto (fixture do motor)', () => {
  it('traz tokens por etapa e por trecho, o orçamento e o que ficou de fora, sem texto do projeto', () => {
    const parsed = contextReviewSchema.parse(contextReviewFixture);

    expect(parsed.calls.map((call) => call.agentId)).toEqual(['planning', 'architecture']);
    expect(parsed.calls[0]?.sections.reduce((total, section) => total + section.tokens, 0)).toBe(
      parsed.calls[0]?.inputTokens
    );
    expect(parsed.plannedTokens).toBe(
      parsed.calls.reduce((total, call) => total + call.inputTokens + call.outputTokens, 0)
    );
    expect(parsed.leftOut).toMatchObject({ cutBySize: 1, truncated: 1, pending: 1 });
    expect(JSON.stringify(contextReviewFixture)).not.toMatch(/TEXTO-DO-PROJETO/);
  });
});

describe('contrato da ligação com a IDE (fixture do motor)', () => {
  it('traz o executável do motor, o argumento do servidor MCP e as ferramentas de leitura', () => {
    const parsed = ideConnectionSchema.parse(ideConnectionFixture);

    expect(parsed.args).toEqual(['mcp']);
    expect(parsed.projectArgument).toBe('--project');
    expect(parsed.command).toMatch(/mariliz-runtime\.exe$/);
    expect(parsed.tools.map((tool) => tool.name)).toEqual([
      'list_projects',
      'project_briefing',
      'decisions',
      'search_project'
    ]);
  });
});

describe('contrato da lista de ferramentas de IA (fixture do motor)', () => {
  it('traz uma entrada por ferramenta, com a versão nula quando ela não a informou', () => {
    const parsed = aiToolListSchema.parse(aiToolsFixture);

    expect(parsed.tools.map((tool) => tool.id)).toEqual(['claude-code', 'cursor', 'codex', 'gemini']);
    expect(parsed.tools.find((tool) => tool.id === 'codex')).toMatchObject({ installed: true, version: null });
    expect(parsed.tools.filter((tool) => tool.harnessSupport).map((tool) => tool.id)).toEqual(['claude-code']);
  });

  it('não traz o caminho do executável', () => {
    expect(JSON.stringify(aiToolsFixture)).not.toMatch(/[\\/]tools[\\/]|\.cmd|\.exe/);
  });
});

describe('contrato da trilha de auditoria do projeto (fixture do motor)', () => {
  it('traz entradas do arquivo e do diário, da mais recente à mais antiga, e as fontes com a contagem', () => {
    const parsed = projectAuditTrailSchema.parse(auditFixture);
    expect([...new Set(parsed.entries.map((entry) => entry.source))].sort()).toEqual(['audit', 'journal']);
    const instants = parsed.entries.map((entry) => entry.at);
    expect([...instants].sort().reverse()).toEqual(instants);
    expect(parsed.sources.map((source) => source.source).sort()).toEqual(['audit', 'journal']);
    expect(parsed.sources.every((source) => source.available)).toBe(true);
    expect(parsed.nextBefore).toBeNull();
  });

  it('cada entrada tem id único e exatamente uma referência: execução do diário ou linha do arquivo', () => {
    const parsed = projectAuditTrailSchema.parse(auditFixture);
    expect(new Set(parsed.entries.map((entry) => entry.id)).size).toBe(parsed.entries.length);
    for (const entry of parsed.entries) {
      if (entry.source === 'journal') {
        expect(entry.runId).not.toBeNull();
        expect(entry.auditLine).toBeNull();
      } else {
        expect(entry.auditLine).not.toBeNull();
        expect(entry.runId).toBeNull();
      }
    }
  });

  it('a contagem de cada categoria bate com as entradas e toda entrada tem categoria conhecida', () => {
    const parsed = projectAuditTrailSchema.parse(auditFixture);
    const known = new Set(parsed.categories.map((category) => category.id));
    for (const category of parsed.categories) {
      expect(parsed.entries.filter((entry) => entry.category === category.id)).toHaveLength(category.count);
    }
    expect(parsed.entries.every((entry) => known.has(entry.category))).toBe(true);
  });

  it('o texto não carrega credencial, texto de pedido nem identificador de item', () => {
    expect(JSON.stringify(auditFixture)).not.toMatch(/acme\/app#|ghp_|sk-/);
  });
});

describe('contrato da investigação de gestão (fixture do motor)', () => {
  it('traz a resposta conferida, o caminho percorrido e o que limitou a investigação', () => {
    const parsed = projectInvestigationSchema.parse(investigationFixture);
    expect(parsed.path.map((step) => step.id)).toEqual(['frame', 'gather', 'conclude']);
    expect(parsed.rounds).toBe(1);
    expect(parsed.usedModel).toBe(true);
    expect(parsed.limitations.map((item) => item.code)).toEqual(['INVESTIGATION_STOPPED']);
    expect(parsed.runId).toBe(parsed.investigationId);
  });

  it('cada afirmação cita fatos que a própria resposta traz e o caminho só tem ids, nunca texto', () => {
    const parsed = projectInvestigationSchema.parse(investigationFixture);
    const cited = new Set(parsed.answer.evidence.map((fact) => fact.id));
    for (const statement of [...parsed.answer.observed, ...parsed.answer.inferences]) {
      expect(statement.evidence.length).toBeGreaterThan(0);
      for (const id of statement.evidence) expect(cited.has(id)).toBe(true);
    }
    for (const step of parsed.path) for (const id of step.facts) expect(id).toMatch(/^[A-Za-z0-9][A-Za-z0-9._:-]*$/);
  });

  it('não carrega pergunta, credencial nem título de item', () => {
    expect(JSON.stringify(investigationFixture)).not.toMatch(/SEGREDO|ghp_|sk-|Titulo secreto/);
  });
});

describe('contrato do assistente do portfólio (fixtures do motor)', () => {
  it('o pacote traz os projetos escolhidos com alias e os fatos de cada um prefixados por ele', () => {
    const parsed = portfolioEvidenceSchema.parse(portfolioEvidenceFixture);
    expect(parsed.projects.map((project) => project.alias)).toEqual(['p1', 'p2']);
    for (const project of parsed.projects) {
      const own = parsed.facts.filter((fact) => fact.id.startsWith(`${project.alias}.`));
      expect(own).toHaveLength(project.facts);
      expect(own[0]!.id).toBe(`${project.alias}.name`);
    }
    expect(parsed.facts.filter((fact) => fact.id === 'today')).toHaveLength(1);
    expect(new Set(parsed.facts.map((fact) => fact.id)).size).toBe(parsed.facts.length);
  });

  it('a resposta cita só fatos de projetos que ela cobre e o texto não carrega a pergunta nem título de item fechado', () => {
    const parsed = portfolioAnswerSchema.parse(portfolioAnswerFixture);
    const aliases = new Set(parsed.projects.map((project) => `${project.alias}.`));
    for (const fact of parsed.answer.evidence)
      expect([...aliases].some((alias) => fact.id.startsWith(alias))).toBe(true);
    expect(parsed.answer.projectId).toBe('portfolio');
    expect(JSON.stringify([portfolioAnswerFixture, portfolioEvidenceFixture])).not.toMatch(
      /SEGREDO|Titulo secreto|ghp_|sk-/
    );
  });

  it('cloud: catálogo só de leitura, conexões sem credencial e leitura com as colunas da capacidade', () => {
    const catalog = cloudCatalogSchema.parse(cloudCatalogFixture);
    expect(catalog.providers.map((provider) => provider.provider).sort()).toEqual(['aws', 'azure', 'gcp']);
    for (const provider of catalog.providers)
      for (const capability of provider.capabilities) {
        expect(capability.impact).toBe('read');
        expect(capability.id.startsWith(`${provider.provider}.`)).toBe(true);
      }
    const connections = cloudConnectionListSchema.parse(cloudConnectionsFixture);
    expect(connections.connections.map((connection) => connection.status)).toEqual([
      'connected',
      'notAuthenticated',
      'expired'
    ]);
    expect(JSON.stringify(cloudConnectionsFixture)).not.toMatch(/secret|password|accessKey|aws_secret/i);
    expect(cloudLinkListSchema.parse(cloudLinksFixture).links[0]?.agentAccess).toBe(true);
    const read = cloudReadResultSchema.parse(cloudReadFixture).result;
    for (const row of read.rows) expect(row).toHaveLength(read.columns.length);
  });

  it('iac: estado sem caminho completo e proposta do OpenTofu só da fonte oficial', () => {
    const status = iacRuntimeStatusResultSchema.parse(iacStatusFixture).runtime;
    expect(status.state).toBe('ready');
    for (const installation of status.installations) expect(installation.path.startsWith('~')).toBe(true);
    expect(status.project?.requirements[0]?.constraint).toBe('>= 1.11');
    const offer = iacRuntimeOfferResultSchema.parse(iacOfferFixture).offer;
    expect(status.installations.map((installation) => installation.engine)).toEqual(['openTofu', 'terraform']);
    expect(offer.engine).toBe('openTofu');
    for (const url of [offer.archiveUrl, offer.shasumsUrl, offer.signatureUrl])
      expect(url.startsWith(`https://github.com/opentofu/opentofu/releases/download/v${offer.version}/`)).toBe(true);
    expect(offer.signingKey).toBe('E3E6E43D84CB852EADB0051D0C0AF313E5FD9F80');
  });

  it('iac: ciclo real de validate, plan, aprovação e apply, sem valores de atributo fora dos outputs', () => {
    expect(iacLayoutSchema.parse(iacLayoutFixture).layout.root).toBe('infra');
    const [pending, apply, applied, validation] = iacOperationListSchema.parse(iacOperationsFixture).operations;
    expect(pending?.summary?.delete).toBe(1);
    expect(pending?.appliedBy).toBeNull();
    expect(apply?.kind).toBe('apply');
    expect(apply?.apply?.verified).toBe(true);
    expect(apply?.approval?.planId).toBe(applied?.planId);
    expect(applied?.appliedBy).toBe(apply?.id);
    expect(validation?.validation?.map((folder) => folder.folder)).toEqual(['infra', 'infra/modules/rede']);
    const withoutOutputs = (iacOperationsFixture.operations as Record<string, unknown>[]).map(
      ({ apply: _apply, ...rest }) => rest
    );
    expect(JSON.stringify(withoutOutputs)).not.toMatch(/fila-dev|10\.0\.0\.0/);
  });

  it('iac: state real por raiz, consulta só de endereços e bootstrap no formato do modelo', () => {
    const report = iacStateReportSchema.parse(iacStateFixture).state;
    const [main, bootstrap] = report.roots;
    expect(main).toMatchObject({ folder: 'infra', backend: 's3', mode: 'remote', locking: true, encrypted: true });
    expect(bootstrap).toMatchObject({
      folder: 'infra/bootstrap',
      mode: 'localInProject',
      localStateFiles: ['terraform.tfstate']
    });
    expect(JSON.stringify(iacStateFixture)).not.toMatch(/access_key|secret_key|sas_token/);
    const read = iacStateReadSchema.parse(iacStateResourcesFixture).state;
    expect(read.addresses).toContain('terraform_data.fila');
    const plan = iacBootstrapPlanSchema.parse(iacBootstrapFixture).plan;
    expect(plan.changes.map((change) => change.path)).toContain('infra/bootstrap/resource.tf');
    expect(plan.backendBlock).toMatch(/use_lockfile\s+= true/);
  });

  it('aceita o desenho proposto e o plano de geração do Terraform no modelo', () => {
    const draft = iacArchitectDraftSchema.parse(iacArchitectDraftFixture).draft;
    expect(draft.valid).toBe(true);
    expect(draft.blueprint?.service?.cpu).toBe(512);
    expect(draft.notes?.services[0]?.name).toBe('ECS Fargate');
    const generated = iacGeneratePlanSchema.parse(iacGeneratePlanFixture).plan;
    expect(generated.root).toBe('infra');
    expect(generated.changes.map((change) => change.path)).toContain('infra/modules/sqs/resource.tf');
    expect(generated.checks.every((check) => check.passed)).toBe(true);
    expect(generated.costs.length).toBeGreaterThan(0);
  });
});
