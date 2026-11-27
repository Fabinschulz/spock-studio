import { InlineNotice, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { formatEngineDate } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useEffect, useId, useRef } from 'react';

import { canResume, listedLabel, listedTone, type BootListItem } from '../domain/boot-labels';
import type { BootApi } from '../service/boot-api';
import styles from './recent-boots.module.scss';

const MAX_ROWS = 5;

interface RecentBootsProps {
  api: BootApi;
  projectId: string;
  /** Há um Boot em preparo: o motor atende um por vez, então retomar e abrir esperam. */
  busy: boolean;
  /** Muda quando a Home mostra um Boot novo: a lista relê para trazer o que acabou de acontecer. */
  refreshKey: string | null;
  onResume: (bootId: string) => Promise<unknown>;
  onOpen: (bootId: string) => Promise<unknown>;
}

const time = (item: BootListItem) => Date.parse(item.createdAt) || 0;

/** Os mais novos primeiro; o que a queda deixou no meio fica à vista mesmo antigo, porque é o que dá para retomar. */
function visible(boots: readonly BootListItem[]): BootListItem[] {
  return [...boots].sort((a, b) => time(b) - time(a)).filter((item, index) => index < MAX_ROWS || canResume(item));
}

/**
 * Os últimos pedidos preparados neste projeto. Um que a queda do aplicativo deixou no meio tem o botão Retomar; os que
 * têm resultado abrem no mesmo cartão da Home. Sem nenhum Boot o bloco não aparece.
 */
export function RecentBoots({ api, projectId, busy, refreshKey, onResume, onOpen }: RecentBootsProps) {
  const titleId = useId();
  const load = useCallback(() => api.list(projectId), [api, projectId]);
  const feed = useAsyncResource(load, { keepPrevious: true });
  const { reload } = feed;

  const seenKey = useRef(refreshKey);
  useEffect(() => {
    if (seenKey.current === refreshKey) return;
    seenKey.current = refreshKey;
    if (refreshKey) reload();
  }, [refreshKey, reload]);

  const resume = async (bootId: string) => {
    // Uma retomada que deu certo já muda `refreshKey`; só a que falhou precisa reler, porque a lista pode estar velha.
    if ((await onResume(bootId)) === null) reload();
  };

  if (feed.status === 'ready' && feed.value.boots.length === 0) return null;

  return (
    <section className={styles.section} aria-labelledby={titleId} aria-busy={feed.status === 'loading'}>
      <h2 id={titleId} className={styles.title}>
        Pedidos recentes
      </h2>
      {feed.status === 'loading' ? <p className={styles.muted}>Buscando os últimos pedidos…</p> : null}
      {feed.status === 'error' ? (
        <InlineNotice tone="danger">
          <p className={styles.muted}>O Spock não conseguiu buscar os últimos pedidos.</p>
          <Button variant="text" size="small" onClick={reload}>
            Tentar de novo
          </Button>
        </InlineNotice>
      ) : null}
      {feed.status === 'ready' ? (
        <ul className={styles.list} role="list" aria-label="Últimos pedidos do projeto">
          {visible(feed.value.boots).map((item) => {
            const title = item.headline ?? item.objective ?? 'Pedido sem resumo';
            const content = (
              <>
                <span className={styles.rowText}>
                  <span className={styles.rowTitle}>{title}</span>
                  <span className={styles.rowMeta}>
                    {formatEngineDate(item.createdAt)}
                    {item.recoveredByBootId ? ' · recuperado' : ''}
                    {item.recoversBootId ? ' · recupera outro' : ''}
                  </span>
                </span>
                <StatusChip tone={listedTone(item)}>{listedLabel(item)}</StatusChip>
              </>
            );
            return (
              <li key={item.id} className={styles.item}>
                {item.headline === null ? (
                  <div className={styles.row}>{content}</div>
                ) : (
                  <button
                    type="button"
                    className={styles.row}
                    disabled={busy}
                    aria-label={`Abrir: ${title.replace(/\.$/, '')}. ${listedLabel(item)}`}
                    onClick={() => void onOpen(item.id)}
                  >
                    {content}
                  </button>
                )}
                {canResume(item) ? (
                  <span className={styles.resume}>
                    <Button
                      variant="outlined"
                      size="small"
                      disabled={busy}
                      aria-label={`Retomar: ${title.replace(/\.$/, '')}`}
                      onClick={() => void resume(item.id)}
                    >
                      Retomar
                    </Button>
                  </span>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}
    </section>
  );
}
