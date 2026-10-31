import { StatusGlyph } from '@/shared/components/ui';
import { PanelSection } from '../panel-section';
import styles from './participants-section.module.scss';
import type { ParticipantsSectionProps } from './types';

export function ParticipantsSection({ participants }: ParticipantsSectionProps) {
  return (
    <PanelSection id="ctx-participantes" title="Participantes">
      <ul className={styles.actors}>
        {participants.map((p) => (
          <li key={p.id} className={styles.actor}>
            <StatusGlyph state={p.glyph} label={p.state} />
            <div className={styles.text}>
              <p className={styles.name}>{p.name}</p>
              <p className={styles.state}>{p.state}</p>
              {p.activity ? <p className={styles.activity}>{p.activity}</p> : null}
              {p.note ? <p className={styles.note}>{p.note}</p> : null}
            </div>
          </li>
        ))}
      </ul>
    </PanelSection>
  );
}
