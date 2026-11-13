import { ConfirmAction, StatusChip, SuccessState } from '@/shared/components';
import type { Connection, ConnectionReach, Connector } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState, type FormEvent } from 'react';

import { formatEngineDate } from '../domain/format-date';
import { CONNECTION_STATUS } from '../domain/labels';
import styles from '../integrations-page.module.scss';
import { ConnectionReachSummary } from './connection-reach';

interface ConnectionRowProps {
  connection: Connection;
  connector: Connector | undefined;
  /** O que a conexão alcança; nulo quando o motor não conseguiu dizer. */
  reach: ConnectionReach | null;
  busy: boolean;
  /** O que deu certo na última ação desta conexão, dito na própria linha. */
  notice?: string | null;
  onTest: () => void;
  /** Devolve verdadeiro quando o provedor aceitou a nova credencial. */
  onRotate: (secret: string) => Promise<boolean>;
  onDisconnect: () => void;
  onRemove: () => void;
}

type Mode = 'idle' | 'rotating' | 'confirming-disconnect' | 'confirming-remove';

export function ConnectionRow({
  connection,
  connector,
  reach,
  busy,
  notice,
  onTest,
  onRotate,
  onDisconnect,
  onRemove
}: ConnectionRowProps) {
  const [mode, setMode] = useState<Mode>('idle');
  const [secret, setSecret] = useState('');
  const status = CONNECTION_STATUS[connection.status];
  const connected = connection.status !== 'disconnected';

  const rotate = async (event: FormEvent) => {
    event.preventDefault();
    if (secret.trim().length < 8) return;
    if (await onRotate(secret.trim())) {
      setSecret('');
      setMode('idle');
    }
  };

  return (
    <li className={styles.row}>
      <div className={styles.rowHead}>
        <div>
          <span className={styles.rowTitle}>{connection.name}</span>
          <span className={styles.meta}>
            {' '}
            · {connector?.name ?? connection.provider} · {connection.account}
          </span>
        </div>
        <StatusChip tone={status.tone}>{status.label}</StatusChip>
      </div>

      <p className={styles.meta}>
        {connection.scopes.length > 0
          ? `Escopos informados: ${connection.scopes.join(', ')}.`
          : (connector?.scopesNote ?? 'O provedor não informa escopos.')}{' '}
        Conferida {formatEngineDate(connection.checkedAt)}.{' '}
        {connection.projects === 0
          ? 'Nenhum projeto vinculado.'
          : `${connection.projects} ${connection.projects === 1 ? 'vínculo' : 'vínculos'} com projetos.`}
      </p>
      <ConnectionReachSummary connection={connection} connector={connector} reach={reach} />
      {connection.lastError ? (
        <p className={styles.error}>
          {connection.lastError.message} ({formatEngineDate(connection.lastError.at)})
        </p>
      ) : null}

      {mode === 'rotating' ? (
        <form className={styles.form} aria-label={`Nova credencial de ${connection.name}`} onSubmit={rotate}>
          <TextField
            label={connector?.secret.label ?? 'Credencial'}
            type="password"
            autoComplete="off"
            fullWidth
            value={secret}
            disabled={busy}
            onChange={(event) => setSecret(event.target.value)}
            helperText="A nova credencial é conferida com o provedor antes de substituir a atual."
            slotProps={{ htmlInput: { spellCheck: false } }}
          />
          <div className={styles.actions}>
            <Button type="submit" variant="contained" disabled={busy || secret.trim().length < 8}>
              Salvar credencial
            </Button>
            <Button type="button" variant="outlined" disabled={busy} onClick={() => setMode('idle')}>
              Cancelar
            </Button>
          </div>
        </form>
      ) : null}

      {mode === 'confirming-disconnect' ? (
        <ConfirmAction
          label={`Desconectar ${connection.name}`}
          confirmLabel="Apagar credencial"
          busy={busy}
          onConfirm={() => {
            setMode('idle');
            onDisconnect();
          }}
          onCancel={() => setMode('idle')}
        >
          A credencial será apagada deste computador. O que já foi sincronizado continua disponível, sem novas leituras.
        </ConfirmAction>
      ) : null}

      {mode === 'confirming-remove' ? (
        <ConfirmAction
          label={`Remover ${connection.name}`}
          confirmLabel="Remover conexão"
          busy={busy}
          onConfirm={() => {
            setMode('idle');
            onRemove();
          }}
          onCancel={() => setMode('idle')}
        >
          A conexão sai da lista e a credencial é apagada deste computador. Para usá-la de novo, será preciso conectar
          outra vez, com uma credencial nova.
        </ConfirmAction>
      ) : null}

      {mode === 'idle' ? (
        <div className={styles.actions}>
          {connected ? (
            <Button type="button" variant="outlined" disabled={busy} onClick={onTest}>
              Testar
            </Button>
          ) : null}
          <Button type="button" variant="outlined" disabled={busy} onClick={() => setMode('rotating')}>
            Atualizar credencial
          </Button>
          {connected ? (
            <Button type="button" variant="text" disabled={busy} onClick={() => setMode('confirming-disconnect')}>
              Desconectar
            </Button>
          ) : null}
          {connection.projects === 0 ? (
            <Button type="button" variant="text" disabled={busy} onClick={() => setMode('confirming-remove')}>
              Remover
            </Button>
          ) : null}
        </div>
      ) : null}

      {notice ? <SuccessState>{notice}</SuccessState> : null}
    </li>
  );
}
