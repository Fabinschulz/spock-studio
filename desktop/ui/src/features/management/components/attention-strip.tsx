import { EmptyState, PageSection, StatusChip } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import { formatEngineDate } from '@/shared/utils';
import { Link } from 'react-router';

import type { Attention } from '../domain/attention';
import styles from '../management.module.scss';

/** Responde primeiro à pergunta da tela, "como está este projeto?", antes de qualquer aba. */
export function AttentionStrip({ attention, oldestReadAt }: { attention: Attention; oldestReadAt: string | null }) {
  return (
    <PageSection title="O que pede atenção">
      {!attention.hasData ? (
        <EmptyState>
          Ainda não há dados sincronizados para dizer o que pede atenção. Sincronize em{' '}
          <Link to={pathTo('integrations')}>Conexões › Serviços</Link>.
        </EmptyState>
      ) : attention.items.length === 0 ? (
        <StatusChip tone="success">Nada pede atenção agora</StatusChip>
      ) : (
        <ul className={styles.signals} aria-label="Pontos de atenção do projeto">
          {attention.items.map((item) => (
            <li key={item.id}>{item.text}</li>
          ))}
        </ul>
      )}
      {oldestReadAt ? (
        <p className={styles.muted}>Leitura mais antiga das fontes: {formatEngineDate(oldestReadAt)}.</p>
      ) : null}
    </PageSection>
  );
}
