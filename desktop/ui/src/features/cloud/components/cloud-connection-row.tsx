import { formatEngineDate } from '@/shared/utils';
import { ConfirmAction, StatusChip } from '@/shared/components';
import type { CloudConnection, CloudProvider, CloudRead } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../cloud-page.module.scss';
import { CLOUD_STATUS, PERMISSION_STATE, capabilityTitle, identityText, permissionSummary } from '../domain/labels';
import { CloudReadTable } from './cloud-read-table';

interface CloudConnectionRowProps {
  connection: CloudConnection;
  provider: CloudProvider | undefined;
  busy: boolean;
  onValidate: () => void;
  onPermissions: () => void;
  onRemove: () => void;
  onRead: (capability: string) => Promise<CloudRead | null>;
}

export function CloudConnectionRow({
  connection,
  provider,
  busy,
  onValidate,
  onPermissions,
  onRemove,
  onRead
}: CloudConnectionRowProps) {
  const [confirming, setConfirming] = useState(false);
  const [showPermissions, setShowPermissions] = useState(false);
  const [read, setRead] = useState<CloudRead | null>(null);
  const status = CLOUD_STATUS[connection.status];
  const mechanism = provider?.mechanisms.find((option) => option.id === connection.settings.mechanism);
  const allowed = connection.permissions.filter((permission) => permission.state === 'allowed');

  return (
    <li className={styles.row}>
      <div className={styles.rowHead}>
        <div>
          <span className={styles.rowTitle}>{connection.name}</span>
          <span className={styles.meta}>
            {' '}
            · {provider?.name ?? connection.provider} · {mechanism?.label ?? connection.settings.mechanism}
          </span>
        </div>
        <StatusChip tone={status.tone}>{status.label}</StatusChip>
      </div>
      <p className={styles.meta}>{identityText(connection)}</p>
      <p className={styles.meta}>
        {connection.settings.values.region ? `Região padrão: ${connection.settings.values.region}. ` : ''}
        {permissionSummary(connection)} Conferida {formatEngineDate(connection.checkedAt)}.{' '}
        {connection.projects === 0
          ? 'Nenhum projeto vinculado.'
          : `${connection.projects} ${connection.projects === 1 ? 'projeto vinculado' : 'projetos vinculados'}.`}
      </p>
      {connection.lastError ? <p className={styles.error}>{connection.lastError.message}</p> : null}

      {showPermissions ? (
        <ul className={styles.permissions} aria-label={`Permissões de ${connection.name}`}>
          {connection.permissions.map((permission) => (
            <li key={permission.capability} style={{ display: 'contents' }}>
              <span>{capabilityTitle(provider, permission.capability)}</span>
              <StatusChip tone={PERMISSION_STATE[permission.state].tone}>
                {PERMISSION_STATE[permission.state].label}
              </StatusChip>
            </li>
          ))}
        </ul>
      ) : null}

      {allowed.length > 0 ? (
        <div className={styles.actions} aria-label={`Explorar ${connection.name}`}>
          {allowed.map((permission) => (
            <Button
              key={permission.capability}
              type="button"
              variant="text"
              disabled={busy}
              onClick={async () => setRead(await onRead(permission.capability))}
            >
              Ver {capabilityTitle(provider, permission.capability)}
            </Button>
          ))}
        </div>
      ) : null}
      {read ? <CloudReadTable read={read} provider={provider} /> : null}

      {confirming ? (
        <ConfirmAction
          label={`Remover ${connection.name}`}
          confirmLabel="Remover conexão"
          busy={busy}
          onConfirm={() => {
            setConfirming(false);
            onRemove();
          }}
          onCancel={() => setConfirming(false)}
        >
          Só o registro sai do Spock Studio. A credencial nunca esteve com ele e continua onde estava.
        </ConfirmAction>
      ) : (
        <div className={styles.actions}>
          <Button type="button" variant="outlined" disabled={busy} onClick={onValidate}>
            Validar
          </Button>
          <Button type="button" variant="outlined" disabled={busy} onClick={onPermissions}>
            Verificar permissões
          </Button>
          <Button
            type="button"
            variant="text"
            disabled={busy}
            onClick={() => setShowPermissions((current) => !current)}
          >
            {showPermissions ? 'Ocultar permissões' : 'Ver permissões'}
          </Button>
          {connection.projects === 0 ? (
            <Button type="button" variant="text" disabled={busy} onClick={() => setConfirming(true)}>
              Remover
            </Button>
          ) : null}
        </div>
      )}
    </li>
  );
}
