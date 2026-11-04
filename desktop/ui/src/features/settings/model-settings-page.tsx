import type { EngineClient } from '@/shared/api';
import { Button, TextField } from '@synthra.io/ui-kit';
import { useId, useMemo, useState } from 'react';

import { useModelSettings } from './hooks/use-model-settings';
import styles from './model-settings-page.module.scss';
import { createModelApi } from './service/model-api';

const KEY_PATTERN = /^[A-Za-z0-9_-]{20,200}$/;

const PROVIDER_LABEL: Record<string, string> = {
  openai: 'OpenAI'
};

function providerLabel(provider: string): string {
  if (!provider) return '';
  return PROVIDER_LABEL[provider] ?? provider;
}

export function ModelSettingsPage({ client }: { client: EngineClient }) {
  const inputId = useId();
  const api = useMemo(() => createModelApi(client), [client]);
  const { status, busy, error, save: saveKey, clear: clearKey } = useModelSettings(api);
  const [key, setKey] = useState('');

  const canSave = KEY_PATTERN.test(key.trim());

  const save = async () => {
    if (canSave && (await saveKey(key.trim()))) setKey('');
  };

  const clear = async () => {
    if (await clearKey()) setKey('');
  };

  const configured = status?.configured ?? false;

  return (
    <div className={styles.page}>
      <header>
        <h1>Configurações</h1>
        <p className={styles.lead}>
          Preferências locais do Mariliz Studio. Nada aqui é gravado no projeto que você autorizou.
        </p>
      </header>

      <section className={styles.panel} aria-labelledby="model-settings-heading">
        <h2 id="model-settings-heading">Chave de modelo</h2>
        <p className={styles.hint}>
          A chave fica só neste computador, fora do projeto. Ela não entra no índice, no journal, nos logs nem nas
          respostas do protocolo. O juiz por modelo não decide sozinho o resultado.
        </p>

        {status ? (
          <p className={styles.status}>
            {configured ? `Provedor configurado: ${providerLabel(status.provider)}.` : 'Nenhuma chave configurada.'}
          </p>
        ) : (
          <p className={styles.status}>A carregar…</p>
        )}

        <div className={styles.field}>
          <TextField
            id={inputId}
            label="Chave de API"
            type="password"
            autoComplete="off"
            fullWidth
            value={key}
            disabled={busy}
            onChange={(event) => setKey(event.target.value)}
            helperText="Entre 20 e 200 caracteres: letras, números, hífen e sublinhado."
            slotProps={{ htmlInput: { spellCheck: false } }}
          />
        </div>

        {error ? (
          <p className={styles.error} role="alert">
            {error}
          </p>
        ) : null}

        <div className={styles.actions}>
          <Button type="button" disabled={busy || !canSave} onClick={() => void save()}>
            Guardar chave
          </Button>
          {configured ? (
            <Button type="button" variant="outlined" disabled={busy} onClick={() => void clear()}>
              Remover chave
            </Button>
          ) : null}
        </div>
      </section>
    </div>
  );
}
