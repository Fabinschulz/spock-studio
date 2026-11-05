import type { RunEvent } from '@/shared/schema';
import type { RunEvalView, RunFeedItem, RunStepView, RunView } from '../types/run';

const withFeed = (run: RunView, at: number, text: string): RunFeedItem[] => [...run.feed, { at, text }];

/** O instante que o motor carimbou no evento; sem ele (motor antigo), o da chegada. */
function instantOf(event: Pick<RunEvent, 'at'>, arrival: number): number {
  const stamped = event.at ? Date.parse(event.at) : Number.NaN;
  return Number.isFinite(stamped) ? stamped : arrival;
}

const pendingStep = (id: string, title: string): RunStepView => ({
  id,
  title,
  status: 'pending',
  detail: null,
  startedAt: null,
  endedAt: null
});

/** As etapas que o motor anunciou, na ordem dele; o que já começou antes do anúncio chegar mantém o seu estado. */
function announcedSteps(run: RunView, announced: unknown): RunStepView[] {
  if (!Array.isArray(announced)) return run.steps;
  const steps = announced.flatMap((item): RunStepView[] => {
    const { id, title } = (item ?? {}) as Record<string, unknown>;
    if (typeof id !== 'string') return [];
    return [run.steps.find((step) => step.id === id) ?? pendingStep(id, typeof title === 'string' ? title : id)];
  });
  return steps.length > 0 ? steps : run.steps;
}

export function applyRunEvent(run: RunView, event: RunEvent, arrival: number): RunView {
  const data = event.data as Record<string, unknown>;
  const text = (key: string) => (typeof data[key] === 'string' ? (data[key] as string) : null);
  const at = instantOf(event, arrival);

  switch (event.type) {
    case 'run.state':
      return { ...run, state: text('state') ?? run.state };
    case 'run.plan':
      return { ...run, steps: announcedSteps(run, data.steps) };
    case 'step.started': {
      const id = text('id') ?? `step-${run.steps.length}`;
      const title = text('title') ?? id;
      const started: RunStepView = { id, title, status: 'running', detail: null, startedAt: at, endedAt: null };
      const known = run.steps.some((s) => s.id === id);
      const steps = known ? run.steps.map((s) => (s.id === id ? started : s)) : [...run.steps, started];
      return { ...run, steps, feed: withFeed(run, at, `Iniciou: ${title}`) };
    }
    case 'step.completed': {
      const id = text('id');
      const step = run.steps.find((s) => s.id === id);
      return {
        ...run,
        steps: run.steps.map((s) =>
          s.id === id ? { ...s, status: text('status') ?? 'completed', detail: text('detail'), endedAt: at } : s
        ),
        feed: step ? withFeed(run, at, `Concluiu: ${step.title}`) : run.feed
      };
    }
    case 'eval.started': {
      const id = text('id');
      if (id === null) return run;
      const started: RunEvalView = {
        id,
        name: text('name') ?? id,
        kind: text('kind') ?? 'deterministic',
        status: 'running'
      };
      const known = run.evals.some((e) => e.id === id);
      return { ...run, evals: known ? run.evals.map((e) => (e.id === id ? started : e)) : [...run.evals, started] };
    }
    case 'eval.completed': {
      const id = text('id');
      const status = text('status');
      if (id === null || status === null) return run;
      return { ...run, evals: run.evals.map((e) => (e.id === id ? { ...e, status } : e)) };
    }
    case 'file.applied':
    case 'file.reverted': {
      const line = `${text('op') ?? text('status') ?? ''} ${text('path') ?? ''}`.trim();
      return { ...run, files: [...run.files, line], feed: withFeed(run, at, `Gravou ${text('path') ?? 'arquivo'}`) };
    }
    default:
      return run;
  }
}
