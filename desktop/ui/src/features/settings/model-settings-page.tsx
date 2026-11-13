import { formatEngineDate } from '@/features/integrations';
import type { EngineClient } from '@/shared/api';
import { Page, StatusChip, SuccessState, type Tone } from '@/shared/components';
import type { ModelConnection } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
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

const STATE: Record<string, { label: string; tone: Tone }> = {
  none: { label: 'Sem chave', tone: 'neutral' },
  untested: { label: 'Ainda não conferida', tone: 'neutral' },
  connected: { label: 'Conectado', tone: 'success' },
  error: { label: 'Com erro', tone: 'danger' }
};

/** Onde a chave está, sem rodeio: no cofre do sistema ou em arquivo, que é o que ainda acontece onde não há cofre. */
function protectionText(protection: string | null): string | null {
  if (protection === 'vault') return 'Guardada com a proteção do sistema operacional.';
  if (protection === 'file')
    return 'Guardada em arquivo neste computador, sem a proteção do sistema: este sistema ainda não tem cofre.';
  return null;
}

export function ModelSettingsPage({ client }: { client: EngineClient }) {
  const inputId = useId();
  const api = useMemo(() => createModelApi(client), [client]);
  const { connection, busy, error, notice, save: saveKey, test, clear: clearKey } = useModelSettings(api);
  const [key, setKey] = useState('');

  const canSave = KEY_PATTERN.test(key.trim());

  const save = async () => {
    if (canSave && (await saveKey(key.trim()))) setKey('');
  };

  const clear = async () => {
    if (await clearKey()) setKey('');
  };

  const configured = connection?.configured ?? false;

  return (
    <Page
      title="Configurações"
      description="Preferências locais do Spock Studio. Nada aqui é gravado no projeto que você autorizou."
    >
      <section className={styles.panel} aria-labelledby="model-settings-heading">
        <h2 id="model-settings-heading">Provedor de IA</h2>
        <p className={styles.hint}>
          A chave fica só neste computador, fora do projeto. Ela não entra no índice, no journal, nos logs nem nas
          respostas do protocolo. O juiz por modelo não decide sozinho o resultado.
        </p>

        {connection ? <ConnectionState connection={connection} /> : <p className={styles.status}>Carregando…</p>}

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
            helperText="Entre 20 e 200 caracteres: letras, números, hífen e sublinhado. A chave é conferida com o provedor antes de ser guardada; se ele recusar, a atual continua valendo."
            slotProps={{ htmlInput: { spellCheck: false } }}
          />
        </div>

        {error ? (
          <p className={styles.error} role="alert">
            {error}
          </p>
        ) : null}
        {notice ? <SuccessState>{notice}</SuccessState> : null}

        <div className={styles.actions}>
          <Button type="button" disabled={busy || !canSave} onClick={() => void save()}>
            Salvar chave
          </Button>
          {configured ? (
            <Button type="button" variant="outlined" disabled={busy} onClick={() => void test()}>
              Conferir com o provedor
            </Button>
          ) : null}
          {configured ? (
            <Button type="button" variant="outlined" disabled={busy} onClick={() => void clear()}>
              Remover chave
            </Button>
          ) : null}
        </div>
        {configured ? (
          <p className={styles.hint}>
            Conferir envia ao provedor uma pergunta curta (&quot;Responda apenas: ok&quot;), sem dado do projeto.
          </p>
        ) : null}
      </section>
    </Page>
  );
}

function ConnectionState({ connection }: { connection: ModelConnection }) {
  const state = STATE[connection.status] ?? { label: connection.status, tone: 'neutral' as const };
  if (!connection.configured) return <p className={styles.status}>Nenhuma chave configurada.</p>;

  const protection = protectionText(connection.protection);
  return (
    <div className={styles.state}>
      <p className={styles.status}>
        Provedor configurado: {providerLabel(connection.provider)}{' '}
        <StatusChip tone={state.tone}>{state.label}</StatusChip>
      </p>
      <p className={styles.hint}>
        Modelo das conversas dos agentes: <span className={styles.mono}>{connection.model}</span>.
        {connection.checkedAt
          ? ` Conferida ${formatEngineDate(connection.checkedAt)}.`
          : ' Nunca foi conferida com o provedor.'}
      </p>
      {protection ? <p className={styles.hint}>{protection}</p> : null}
      {connection.lastError ? <p className={styles.error}>{connection.lastError.message}</p> : null}
    </div>
  );
}
