import { StatusChip, type Tone } from '@/shared/components';
import { executionPath, pathTo } from '@/shared/routing';
import type { ProjectTimelineEvent } from '@/shared/schema';
import { formatEngineDate } from '@/shared/utils';
import { Link } from 'react-router';

import styles from '../timeline-page.module.scss';

/** Um tipo que a tela não conhece aparece com o nome que veio. */
const KIND_LABEL: Record<string, string> = {
  harness: 'Harness',
  profile: 'Perfil',
  run: 'Execução',
  decision: 'Decisão',
  risk: 'Risco',
  sync: 'Sincronização'
};

/** A cor reforça, mas a palavra diz (nunca só a cor). `info` não leva aviso. */
const LEVEL: Record<string, { label: string; tone: Tone } | undefined> = {
  ok: { label: 'Deu certo', tone: 'success' },
  attention: { label: 'Atenção', tone: 'warning' },
  problem: { label: 'Problema', tone: 'danger' }
};

/** Do mais recente ao mais antigo, na ordem em que o motor entrega. */
export function TimelineEvents({ events, label }: { events: readonly ProjectTimelineEvent[]; label: string }) {
  return (
    <ol className={styles.events} aria-label={label}>
      {events.map((event) => (
        <EventItem key={event.id} event={event} />
      ))}
    </ol>
  );
}

function EventItem({ event }: { event: ProjectTimelineEvent }) {
  const level = LEVEL[event.level];
  return (
    <li className={styles.event}>
      <div className={styles.eventHead}>
        <span className={styles.when}>{formatEngineDate(event.at)}</span>
        <StatusChip tone="neutral">{KIND_LABEL[event.kind] ?? event.kind}</StatusChip>
        {level ? <StatusChip tone={level.tone}>{level.label}</StatusChip> : null}
      </div>
      <p className={styles.title}>{event.title}</p>
      {event.detail ? <p className={styles.meta}>{event.detail}</p> : null}
      {event.refType === 'run' && event.refId ? <Link to={executionPath(event.refId)}>Ver execução</Link> : null}
      {event.refType === 'decision' ? <Link to={pathTo('management')}>Ver em Acompanhamento</Link> : null}
    </li>
  );
}
