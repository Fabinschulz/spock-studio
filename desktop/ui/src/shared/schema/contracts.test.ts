import { describe, expect, it } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import applyResult from '@contracts/fixtures/golden/apply-result.json';
import discovery from '@contracts/fixtures/golden/discovery.json';
import plan from '@contracts/fixtures/golden/plan.json';
import revertResult from '@contracts/fixtures/golden/revert-result.json';
import validateResult from '@contracts/fixtures/golden/validate-result.json';
import evalCompleted from '@contracts/fixtures/valid/event.eval.completed.json';
import evalStarted from '@contracts/fixtures/valid/event.eval.started.json';
import runPlan from '@contracts/fixtures/valid/event.run.plan.json';
import stepStarted from '@contracts/fixtures/valid/event.step.started.json';
import workflowStarted from '@contracts/fixtures/valid/event.workflow.started.json';

import {
  discoverySchema,
  planSchema,
  runEventSchema,
  runResultSchema,
  runSnapshotSchema,
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

  it('workflow.start — parâmetros e resultado alinhados ao contrato', () => {
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
