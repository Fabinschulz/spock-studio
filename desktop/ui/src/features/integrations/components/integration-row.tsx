import { ConfirmAction, StatusChip } from '@/shared/components';
import type { Connector, ProjectIntegration, SyncExecution } from '@/shared/schema';
import { Button, Checkbox } from '@mariliz/ui-kit';
import { useId, useState } from 'react';

import { formatEngineDate } from '../domain/format-date';
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
                    onConfigure({
                      dataKinds: checked
                        ? [...scope.dataKinds, kind.id]
                        : scope.dataKinds.filter((id) => id !== kind.id)
                    })
                  }
                />
              );
            })}
            <p className={styles.hint}>
              Desmarcar um tipo apaga na hora os itens desse tipo já sincronizados. Pelo menos um tipo fica marcado:
              para não ler mais nada, desvincule.
            </p>
            <Checkbox
              label="Permitir que estes dados sirvam de contexto para os agentes"
              checked={policy.feedsKnowledge}
              onChange={(_, checked) => onConfigure({ feedsKnowledge: checked })}
            />
            <p className={styles.hint}>
              Desligado, o que foi sincronizado fica só nas visões de acompanhamento. Nesta versão nenhum agente consome
              dados sincronizados; a permissão fica registrada para quando consumir.
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
