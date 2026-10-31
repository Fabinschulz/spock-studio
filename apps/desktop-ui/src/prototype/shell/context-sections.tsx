import type { FeedItem } from '../domain/describe-event';
import { formatClock } from '../domain/describe-event';
import type { Actor, ActorState } from '../domain/operation-model';
import { Fact, Section, StatusGlyph, Tag, type GlyphState } from '../ui/primitives';

import styles from './context-sections.module.scss';

const actorLabel: Record<ActorState, string> = {
  working: 'Em atividade',
  waiting: 'Aguardando',
  not_started: 'Não iniciado',
  done: 'Concluído',
  failed: 'Falhou'
};

const actorGlyph: Record<ActorState, GlyphState> = {
  working: 'running',
  waiting: 'pending',
  not_started: 'pending',
  done: 'done',
  failed: 'failed'
};

export interface Participant extends Pick<Actor, 'id' | 'name' | 'state' | 'activity'> {
  /** Onde a atividade acontece, quando não é no motor (ex.: o ambiente de IA da pessoa). */
  note?: string;
  demo?: boolean;
}

export function SummarySection({ facts }: { facts: [string, string][] }) {
  return (
    <Section title="Resumo" id="resumo">
      <dl className={styles.facts}>
        {facts.map(([label, value]) => (
          <Fact key={label} label={label}>
            {value}
          </Fact>
        ))}
      </dl>
    </Section>
  );
}

/**
 * Quem está agindo. Cada participante é um fato: o motor local (que publica eventos reais) ou um agente que só
 * aparece se houver evento dele. Sem evento, a interface diz que não há informação, não inventa atividade.
 */
export function ParticipantsSection({ participants, empty }: { participants: Participant[]; empty?: string }) {
  return (
    <Section title="Participantes" id="participantes">
      {participants.length === 0 ? <p className={styles.empty}>{empty}</p> : null}
      <ul role="list" className={styles.actors}>
        {participants.map((p) => (
          <li key={p.id} className={styles.actor}>
            <StatusGlyph state={actorGlyph[p.state]} label={actorLabel[p.state]} />
            <div className={styles.actorText}>
              <p className={styles.actorName}>
                {p.name} {p.demo ? <Tag tone="warning">demonstração</Tag> : null}
              </p>
              <p className={styles.actorState}>{actorLabel[p.state]}</p>
              {p.activity ? <p className={styles.actorActivity}>{p.activity}</p> : null}
              {p.note ? <p className={styles.actorNote}>{p.note}</p> : null}
            </div>
          </li>
        ))}
      </ul>
    </Section>
  );
}

export function ActivitySection({ items, live }: { items: FeedItem[]; live: boolean }) {
  const recent = items.slice(-7).reverse();
  // Eventos do contrato proposto ainda não existem no motor: a seção inteira avisa, em vez de marcar cada linha.
  const proposed = items.some((item) => item.proposed);
  return (
    <Section title="Atividade" id="atividade" aside={proposed ? 'demonstração' : live ? 'ao vivo' : undefined}>
      {recent.length === 0 ? <p className={styles.empty}>Nenhum evento ainda.</p> : null}
      {/* Lista, não log: uma linha por fato, mais recente primeiro. O resumo "ao vivo" é anunciado por região própria na tela. */}
      <ol role="list" className={styles.feed}>
        {recent.map((item) => (
          <li key={item.key} className={styles.feedItem}>
            <time className={styles.feedTime}>{formatClock(item.at)}</time>
            <span className={styles.feedText}>{item.text}</span>
          </li>
        ))}
      </ol>
    </Section>
  );
}
