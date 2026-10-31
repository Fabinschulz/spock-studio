import { StatusGlyph, type GlyphState } from '@/shared/components/ui';
import type { ConfigurationPlan, EvalResult } from '@/shared/schema';

import { aiEnvironmentNames } from '../domain/ai-environments';
import { HARNESS_ENGINEER_NOT_INVOKED } from '../domain/harness-engineer-prompt';
import type { JourneyResults, JourneyStepId, Loadable, RunView } from '../domain/journey-state';
import { formatElapsed, runningStep } from '../domain/run-progress';
import styles from './context-panel.module.scss';

interface ContextPanelProps {
  step: JourneyStepId;
  projectName: string;
  aiEnvironment: string | null;
  plan: Loadable<ConfigurationPlan>;
  approvalCount: number;
  run: RunView | null;
  results: JourneyResults;
}

interface Participant {
  id: string;
  name: string;
  glyph: GlyphState;
  state: string;
  activity?: string | null;
  note?: string;
}

const evalParticipant = (evals: readonly EvalResult[]): Participant | null => {
  const judge = evals.find((e) => e.kind === 'model');
  if (!judge) return null;
  const byStatus: Record<EvalResult['status'], Pick<Participant, 'glyph' | 'state'>> = {
    passed: { glyph: 'done', state: 'Concluído' },
    failed: { glyph: 'failed', state: 'Reprovou' },
    blocked: { glyph: 'warning', state: 'Bloqueado' },
    not_run: { glyph: 'pending', state: 'Não executado' }
  };
  return {
    id: judge.id,
    name: judge.name,
    ...byStatus[judge.status],
    activity: judge.status === 'not_run' ? 'Não foi executado nesta validação.' : null
  };
};

/**
 * Quem está agindo, e só fatos: o motor local (que publica eventos) e os participantes cujo estado o motor informa
 * (a proposta do Harness Engineer, o juiz por modelo). Atividade de agente sem evento do motor não aparece.
 */
function participantsOf({ step, plan, run, results }: ContextPanelProps): Participant[] {
  const apply = results.apply;
  const running = run ? runningStep(run) : null;

  let engine: Participant = { id: 'engine', name: 'Motor Mariliz', glyph: 'pending', state: 'Aguardando' };
  if (run && run.error === null) {
    engine = {
      id: 'engine',
      name: 'Motor Mariliz',
      glyph: 'running',
      state: 'Em atividade',
      activity: running?.title ?? 'Aguardando a próxima etapa'
    };
  } else if (run) {
    engine = { id: 'engine', name: 'Motor Mariliz', glyph: 'failed', state: 'Falhou' };
  } else if (apply && step === 'result') {
    const ok = apply.outcome === 'completed' || apply.outcome === 'completed_with_limitations';
    engine = {
      id: 'engine',
      name: 'Motor Mariliz',
      glyph: ok ? 'done' : 'failed',
      state: ok ? 'Concluído' : 'Falhou',
      activity: null
    };
  }
  engine.note = 'Executa localmente, no seu computador.';

  const list: Participant[] = [engine];

  if (plan.status === 'ready') {
    const waiting = plan.data.limitations.some((l) => l.code === HARNESS_ENGINEER_NOT_INVOKED);
    list.push({
      id: 'harness-engineer',
      name: 'Harness Engineer',
      glyph: waiting ? 'pending' : 'done',
      state: waiting ? 'Aguardando proposta' : 'Proposta recebida',
      activity: waiting ? 'Sem proposta: o plano usa as regras-base.' : null,
      note: 'Roda no seu ambiente de IA, fora do Mariliz. O motor só lê .mariliz/proposal.json.'
    });
  }

  const judge = evalParticipant((results.validate ?? apply)?.validation?.results ?? []);
  if (judge && step === 'result') list.push(judge);

  return list;
}

export function ContextPanel(props: ContextPanelProps) {
  const { projectName, aiEnvironment, plan, approvalCount, step, run } = props;
  const facts: [string, string][] = [
    ['Projeto', projectName],
    ['Modelo', 'Mariliz'],
    ['Ambiente de IA', aiEnvironment ? (aiEnvironmentNames[aiEnvironment] ?? aiEnvironment) : 'a escolher'],
    ['Plano', plan.status === 'ready' ? `${plan.data.changes.length} alterações` : 'ainda não gerado'],
    ['Aprovados', step === 'review' || step === 'run' || step === 'result' ? `${approvalCount}` : '—']
  ];
  const participants = participantsOf(props);
  const feed = run ? run.feed.slice(-7).reverse() : [];

  return (
    <>
      <section className={styles.section} aria-labelledby="ctx-resumo">
        <h3 id="ctx-resumo" className={styles.title}>
          Resumo
        </h3>
        <dl className={styles.facts}>
          {facts.map(([label, value]) => (
            <div key={label} className={styles.fact}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className={styles.section} aria-labelledby="ctx-participantes">
        <h3 id="ctx-participantes" className={styles.title}>
          Participantes
        </h3>
        <ul className={styles.actors}>
          {participants.map((p) => (
            <li key={p.id} className={styles.actor}>
              <StatusGlyph state={p.glyph} label={p.state} />
              <div className={styles.actorText}>
                <p className={styles.actorName}>{p.name}</p>
                <p className={styles.actorState}>{p.state}</p>
                {p.activity ? <p className={styles.actorActivity}>{p.activity}</p> : null}
                {p.note ? <p className={styles.actorNote}>{p.note}</p> : null}
              </div>
            </li>
          ))}
        </ul>
      </section>

      {run ? (
        <section className={styles.section} aria-labelledby="ctx-atividade">
          <h3 id="ctx-atividade" className={styles.title}>
            Atividade
          </h3>
          {feed.length === 0 ? <p className={styles.empty}>Nenhum evento ainda.</p> : null}
          <ol className={styles.feed}>
            {feed.map((item, index) => (
              <li key={`${item.at}-${index}`} className={styles.feedItem}>
                <time className={styles.feedTime}>{formatElapsed(item.at - run.startedAt)}</time>
                <span className={styles.feedText}>{item.text}</span>
              </li>
            ))}
          </ol>
        </section>
      ) : null}
    </>
  );
}
