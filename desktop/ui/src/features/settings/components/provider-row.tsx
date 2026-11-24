import { formatEngineDate } from '@/shared/utils';
import { ConfirmAction, StatusChip } from '@/shared/components';
import type { ProviderConnection } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useId, useState } from 'react';

import { describeProviderState, protectionText } from '../domain/provider-labels';
import styles from './provider-row.module.scss';

const KEY_PATTERN = /^[A-Za-z0-9_-]{20,200}$/;

interface ProviderRowProps {
  provider: ProviderConnection;
  busy: boolean;
  onSave: (provider: ProviderConnection, key: string) => Promise<boolean>;
  onTest: (provider: ProviderConnection) => void;
  onSetDefault: (provider: ProviderConnection) => void;
  onRemove: (provider: ProviderConnection) => void;
}

export function ProviderRow({ provider, busy, onSave, onTest, onSetDefault, onRemove }: ProviderRowProps) {
  const inputId = useId();
  const [key, setKey] = useState('');
  const [removing, setRemoving] = useState(false);
  const state = describeProviderState(provider.status);
  const protection = protectionText(provider.protection);
  const canSave = KEY_PATTERN.test(key.trim());

  const save = async () => {
    if (canSave && (await onSave(provider, key.trim()))) setKey('');
  };

  return (
    <li className={styles.provider}>
      <div className={styles.header}>
        <h3 className={styles.name}>{provider.name}</h3>
        <StatusChip tone={state.tone}>{state.label}</StatusChip>
        {provider.isDefault ? <StatusChip tone="info">Padrão</StatusChip> : null}
      </div>

      {provider.configured ? (
        <div className={styles.details}>
          <p className={styles.hint}>
            Modelo padrão deste provedor: <span className={styles.mono}>{provider.model}</span>.{' '}
            {provider.checkedAt
              ? `Conferida ${formatEngineDate(provider.checkedAt)}.`
              : 'Nunca foi conferida com o provedor.'}
          </p>
          {protection ? <p className={styles.hint}>{protection}</p> : null}
          {provider.lastError ? <p className={styles.error}>{provider.lastError.message}</p> : null}
        </div>
      ) : (
        <p className={styles.hint}>Sem chave neste computador.</p>
      )}

      <TextField
        id={inputId}
        label={`Chave de API (${provider.name})`}
        type="password"
        autoComplete="off"
        fullWidth
        value={key}
        disabled={busy}
        onChange={(event) => setKey(event.target.value)}
        helperText="Entre 20 e 200 caracteres: letras, números, hífen e sublinhado."
        slotProps={{ htmlInput: { spellCheck: false } }}
      />

      <div className={styles.actions}>
        <Button
          type="button"
          disabled={busy || !canSave}
          aria-label={`${provider.configured ? 'Trocar' : 'Salvar'} a chave de ${provider.name}`}
          onClick={() => void save()}
        >
          {provider.configured ? 'Trocar chave' : 'Salvar chave'}
        </Button>
        {provider.configured ? (
          <>
            <Button
              type="button"
              variant="outlined"
              disabled={busy}
              aria-label={`Conferir ${provider.name} com o provedor`}
              onClick={() => onTest(provider)}
            >
              Conferir
            </Button>
            {provider.isDefault ? null : (
              <Button
                type="button"
                variant="outlined"
                disabled={busy}
                aria-label={`Tornar ${provider.name} o provedor padrão`}
                onClick={() => onSetDefault(provider)}
              >
                Tornar padrão
              </Button>
            )}
            <Button
              type="button"
              variant="outlined"
              disabled={busy || removing}
              aria-label={`Remover a chave de ${provider.name}`}
              onClick={() => setRemoving(true)}
            >
              Remover
            </Button>
          </>
        ) : null}
      </div>
      {removing ? (
        <ConfirmAction
          label={`Remover a chave de ${provider.name}`}
          confirmLabel="Remover chave"
          busy={busy}
          onConfirm={() => {
            setRemoving(false);
            onRemove(provider);
          }}
          onCancel={() => setRemoving(false)}
        >
          O que usa {provider.name} (o Assistente, a pergunta ao portfólio e o modelo nos pedidos de engenharia, quando
          ele é o escolhido) para de funcionar até você salvar outra chave.
        </ConfirmAction>
      ) : null}
    </li>
  );
}
