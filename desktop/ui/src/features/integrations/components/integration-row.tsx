import { formatEngineDate } from '@/shared/utils';
import { ConfirmAction, StatusChip } from '@/shared/components';
import type { Connector, ProjectIntegration, SyncExecution } from '@/shared/schema';
import { Button, Checkbox } from '@mariliz/ui-kit';
import { useId, useState } from 'react';

import { CONNECTION_STATUS, SYNC_STATUS, itemsSummary, syncCounts } from '../domain/labels';
import styles from '../integrations-page.module.scss';
import type { ScopeChange } from '../service/integration-api';
import { SyncHistory } from './sync-history';

interface IntegrationRowProps {
  integration: ProjectIntegration;
  connector: Connector | undefined;
  busy: boolean;
  onSync: () => void;
  onConfigure: (change: Pick<ScopeChange, 'dataKinds' | 'feedsKnowledge'>) => void;
  onUnlink: () => void;
  loadHistory: () => Promise<SyncExecution[]>;
}

type Panel = 'none' | 'scope' | 'history' | 'confirming-unlink';
type DataKind = Connector['dataKinds'][number];

function removalText(count: number, label: string): string {
  if (count === 0) return `Ainda não há itens de ${label} sincronizados; o projeto só deixa de ler esse tipo.`;
  const removed =
    count === 1 ? '1 item já sincronizado será apagado' : `${count} itens já sincronizados serão apagados`;
  return `${removed} deste computador (${label}). Nada muda no provedor.`;
}

export function IntegrationRow({
  integration,
  connector,
  busy,
  onSync,
  onConfigure,
  onUnlink,
  loadHistory
}: IntegrationRowProps) {
  const [panel, setPanel] = useState<Panel>('none');
  const [removing, setRemoving] = useState<DataKind | null>(null);
  const panelId = useId();
  const { scope, policy, lastSync, syncing, items } = integration;
  const usable = integration.connectionStatus === 'connected';
  const toggle = (target: Panel) => setPanel((current) => (current === target ? 'none' : target));

  return (
    <li className={styles.row}>
      <div className={styles.rowHead}>
        <div>
          <span className={styles.rowTitle}>{scope.resourceName}</span>
          <span className={styles.meta}>
            {' '}
            · {connector?.name ?? integration.provider} · conexão {integration.connectionName}
          </span>
        </div>
        {syncing ? (
          <StatusChip tone="info">Sincronizando</StatusChip>
        ) : lastSync ? (
          <StatusChip tone={SYNC_STATUS[lastSync.status].tone}>{SYNC_STATUS[lastSync.status].label}</StatusChip>
        ) : null}
      </div>

      <p className={styles.meta}>
        {[
          itemsSummary(integration, connector),
          items.lastSyncedAt ? `lido ${formatEngineDate(items.lastSyncedAt)}` : null,
          lastSync && !syncing ? `última sincronização: ${syncCounts(lastSync)}` : null
        ]
          .filter((part) => part !== null)
          .join(' · ')}
      </p>
      {lastSync?.error && !syncing ? <p className={styles.error}>{lastSync.error.message}</p> : null}
      {!usable ? (
        <p className={styles.meta}>
          A conexão está {CONNECTION_STATUS[integration.connectionStatus].label.toLowerCase()}: os dados acima são da
          última leitura. Para voltar a sincronizar, resolva a conexão {integration.connectionName} em Conexões.
        </p>
      ) : null}

      <div className={styles.actions}>
        <Button type="button" variant="contained" disabled={busy || syncing || !usable} onClick={onSync}>
          Sincronizar
        </Button>
        <Button
          type="button"
          variant="outlined"
          aria-expanded={panel === 'scope'}
          aria-controls={panelId}
          onClick={() => toggle('scope')}
        >
          Ajustar recorte
        </Button>
        <Button
          type="button"
          variant="outlined"
          aria-expanded={panel === 'history'}
          aria-controls={panelId}
          onClick={() => toggle('history')}
        >
          Histórico
        </Button>
        <Button type="button" variant="text" disabled={busy || syncing} onClick={() => toggle('confirming-unlink')}>
          Desvincular
        </Button>
      </div>

      <div id={panelId}>
        {panel === 'scope' ? (
          <fieldset className={styles.kinds} disabled={busy || syncing}>
            <legend>Dados que este projeto pode ler de {scope.resourceName}</legend>
            {(connector?.dataKinds ?? []).map((kind) => {
              const enabled = scope.dataKinds.includes(kind.id);
              return (
                <Checkbox
                  key={kind.id}
                  label={kind.label}
                  checked={enabled}
                  disabled={enabled && scope.dataKinds.length === 1}
                  onChange={(_, checked) =>
                    checked ? onConfigure({ dataKinds: [...scope.dataKinds, kind.id] }) : setRemoving(kind)
                  }
                />
              );
            })}
            {removing ? (
              <ConfirmAction
                label={`Parar de ler ${removing.label}`}
                confirmLabel="Parar de ler e apagar"
                busy={busy}
                onConfirm={() => {
                  onConfigure({ dataKinds: scope.dataKinds.filter((id) => id !== removing.id) });
                  setRemoving(null);
                }}
                onCancel={() => setRemoving(null)}
              >
                {removalText(items.byKind[removing.id] ?? 0, removing.label)}
              </ConfirmAction>
            ) : null}
            <p className={styles.hint}>Pelo menos um tipo fica marcado: para não ler mais nada, desvincule.</p>
            <Checkbox
              label="Permitir que estes dados sirvam de contexto para os agentes"
              checked={policy.feedsKnowledge}
              onChange={(_, checked) => onConfigure({ feedsKnowledge: checked })}
            />
            <p className={styles.hint}>
              Ligado, os itens entram no índice do projeto e podem chegar ao provedor de IA quando um agente ou o
              assistente os usa, marcados como fonte externa e sem segredos. Desligado, ficam só nas visões de
              acompanhamento. O detalhe do que vai está em Conhecimento.
            </p>
          </fieldset>
        ) : null}

        {panel === 'history' ? <SyncHistory key={`${lastSync?.id}:${lastSync?.status}`} load={loadHistory} /> : null}

        {panel === 'confirming-unlink' ? (
          <ConfirmAction
            label={`Desvincular ${scope.resourceName}`}
            confirmLabel="Desvincular e apagar"
            busy={busy}
            onConfirm={onUnlink}
            onCancel={() => setPanel('none')}
          >
            O vínculo e {items.total === 1 ? 'o item sincronizado' : `os ${items.total} itens sincronizados`} por ele
            serão apagados deste computador. Nada muda no provedor.
          </ConfirmAction>
        ) : null}
      </div>
    </li>
  );
}
