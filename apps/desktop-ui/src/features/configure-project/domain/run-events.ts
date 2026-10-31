import type { RunEvent } from '@/shared/schema';
import type { RunFeedItem, RunStepView, RunView } from '../types/run';

const withFeed = (run: RunView, at: number, text: string): RunFeedItem[] => [...run.feed, { at, text }];

export function applyRunEvent(run: RunView, event: RunEvent, at: number): RunView {
  const data = event.data as Record<string, unknown>;
  const text = (key: string) => (typeof data[key] === 'string' ? (data[key] as string) : null);

  switch (event.type) {
    case 'run.state':
      return { ...run, state: text('state') ?? run.state };
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
    case 'file.applied':
    case 'file.reverted': {
      const line = `${text('op') ?? text('status') ?? ''} ${text('path') ?? ''}`.trim();
      return { ...run, files: [...run.files, line], feed: withFeed(run, at, `Gravou ${text('path') ?? 'arquivo'}`) };
    }
    default:
      return run;
  }
}
