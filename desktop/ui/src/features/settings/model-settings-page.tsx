import type { EngineClient } from '@/shared/api';
import { EngineError } from '@/shared/api';
import { modelStatusSchema } from '@/shared/schema';
import { Button } from '@synthra.io/ui-kit';
import { useEffect, useId, useState } from 'react';

import styles from './model-settings-page.module.scss';

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
  const [status, setStatus] = useState<{ configured: boolean; provider: string } | null>(null);
  const [key, setKey] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void client
      .request('model.status', {}, modelStatusSchema)
      .then((next) => {
        if (active) setStatus(next);
      })
      .catch((cause) => {
        if (active) {
          setError(cause instanceof EngineError ? cause.message : 'Não foi possível ler o estado do modelo.');
        }
      });
    return () => {
      active = false;
    };
  }, [client]);

  const canSave = KEY_PATTERN.test(key.trim());

  const save = async () => {
    if (!canSave) return;
    setBusy(true);
    setError(null);
    try {
      const next = await client.request('model.save', { provider: 'openai', key: key.trim() }, modelStatusSchema);
      setStatus(next);
      setKey('');
    } catch (cause) {
      setError(cause instanceof EngineError ? cause.message : 'Não foi possível guardar a chave.');
    } finally {
      setBusy(false);
    }
  };

  const clear = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = await client.request('model.clear', {}, modelStatusSchema);
      setStatus(next);
      setKey('');
    } catch (cause) {
      setError(cause instanceof EngineError ? cause.message : 'Não foi possível remover a chave.');
    } finally {
      setBusy(false);
    }
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
          <label htmlFor={inputId}>Chave de API</label>
          <input
            id={inputId}
            className={styles.input}
            type="password"
            autoComplete="off"
            spellCheck={false}
            value={key}
            disabled={busy}
            onChange={(event) => setKey(event.target.value)}
          />
          <p className={styles.hint}>Entre 20 e 200 caracteres: letras, números, hífen e sublinhado.</p>
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
