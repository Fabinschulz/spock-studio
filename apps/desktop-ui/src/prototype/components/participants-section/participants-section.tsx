import { StatusGlyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { ACTOR_GLYPH, ACTOR_LABEL } from '@/prototype/constants/actors';

import styles from './participants-section.module.scss';
import type { ParticipantsSectionProps } from './types';

// Cada participante é um fato: sem evento, não há atividade inventada.
export function ParticipantsSection({ participants, empty }: ParticipantsSectionProps) {
  return (
    <Section title="Participantes" id="participantes">
      {participants.length === 0 ? <p className={styles.empty}>{empty}</p> : null}
      <ul role="list" className={styles.actors}>
        {participants.map((p) => (
          <li key={p.id} className={styles.actor}>
            <StatusGlyph state={ACTOR_GLYPH[p.state]} label={ACTOR_LABEL[p.state]} />
            <div className={styles.actorText}>
              <p className={styles.actorName}>
                {p.name} {p.demo ? <Tag tone="warning">demonstração</Tag> : null}
              </p>
              <p className={styles.actorState}>{ACTOR_LABEL[p.state]}</p>
              {p.activity ? <p className={styles.actorActivity}>{p.activity}</p> : null}
              {p.note ? <p className={styles.actorNote}>{p.note}</p> : null}
            </div>
          </li>
        ))}
      </ul>
    </Section>
  );
}
