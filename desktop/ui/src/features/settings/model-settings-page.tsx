import { formatEngineDate } from '@/features/integrations';
import type { EngineClient } from '@/shared/api';
import { ConfirmAction, LoadingState, Page, StatusChip, SuccessState, Term, type Tone } from '@/shared/components';
import type { ModelConnection, ModelPurpose } from '@/shared/schema';
import { Button, SelectField, TextField } from '@mariliz/ui-kit';
import { useId, useMemo, useState } from 'react';

import { useModelSettings } from './hooks/use-model-settings';
import styles from './model-settings-page.module.scss';
import { createModelApi } from './service/model-api';

const KEY_PATTERN = /^[A-Za-z0-9_-]{20,200}$/;

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
    return 'Guardada em arquivo neste computador, sem a proteção do sistema: não há cofre disponível aqui. A tela de Integrações diz o que falta.';
  return null;
}

export function ModelSettingsPage({ client }: { client: EngineClient }) {
  const inputId = useId();
  const api = useMemo(() => createModelApi(client), [client]);
  const { connection, busy, error, notice, save: saveKey, test, choose, clear: clearKey } = useModelSettings(api);
  const [key, setKey] = useState('');
  const [chosen, setChosen] = useState<string | null>(null);
  const [removing, setRemoving] = useState(false);

  const canSave = KEY_PATTERN.test(key.trim());
  const providers = connection?.available ?? [];
  // O provedor escolhido na tela; sem escolha, o da chave guardada e, sem chave, o primeiro da lista.
  const provider = providers.find((entry) => entry.id === (chosen ?? connection?.provider)) ?? providers[0];
  const switching = connection?.configured === true && provider !== undefined && provider.id !== connection.provider;

  const save = async () => {
    if (canSave && provider && (await saveKey(provider.id, key.trim()))) {
      setKey('');
      setChosen(null);
    }
  };

  const clear = async () => {
    if (await clearKey()) setKey('');
  };

  const configured = connection?.configured ?? false;

  return (
    <Page
      title="Minha IA"
      description={
        <>
          O <Term id="provider" /> e o <Term id="model" /> que o Spock Studio usa. Nada aqui é gravado no projeto que
          você autorizou.
        </>
      }
    >
      <section className={styles.panel} aria-labelledby="model-settings-heading">
        <h2 id="model-settings-heading">Provedor de IA</h2>
        <p className={styles.hint}>
          A <Term id="apiKey" /> fica só neste computador, fora do projeto. Ela nunca aparece no índice do projeto, nos
          registros das execuções nem no diagnóstico. Nas validações, a avaliação do modelo nunca decide o resultado
          sozinha.
        </p>

        {connection ? <ConnectionState connection={connection} /> : <LoadingState>Lendo a configuração…</LoadingState>}

        {providers.length > 0 ? (
          <div className={styles.field}>
            <SelectField
              label="Provedor"
              size="small"
              fullWidth
              value={provider?.id ?? ''}
              disabled={busy}
              onChange={(event) => setChosen(String(event.target.value))}
              options={providers.map((entry) => ({ label: entry.name, value: entry.id }))}
            />
            {provider ? (
              <p className={styles.hint}>
                Com {provider.name}, as conversas dos agentes usam o modelo{' '}
                <span className={styles.mono}>{provider.model}</span>.
              </p>
            ) : null}
            {switching ? (
              <p className={styles.hint}>
                Trocar de provedor substitui a chave de {connection.providerName}: só uma chave fica ativa. Se o novo
                provedor recusar a chave, a atual continua valendo.
              </p>
            ) : null}
          </div>
        ) : null}

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
            <Button type="button" variant="outlined" disabled={busy || removing} onClick={() => setRemoving(true)}>
              Remover chave
            </Button>
          ) : null}
        </div>
        {removing ? (
          <ConfirmAction
            label="Remover a chave"
            confirmLabel="Remover chave"
            busy={busy}
            onConfirm={() => {
              setRemoving(false);
              void clear();
            }}
            onCancel={() => setRemoving(false)}
          >
            Sem a chave, o Assistente, a pergunta ao portfólio e o uso do modelo nos pedidos de engenharia param de
            funcionar até você salvar outra.
          </ConfirmAction>
        ) : null}
        {configured ? (
          <p className={styles.hint}>
            Conferir envia ao provedor uma pergunta curta (&quot;Responda apenas: ok&quot;), sem dado do projeto.
          </p>
        ) : null}
      </section>

      {connection?.configured ? (
        <section className={styles.panel} aria-labelledby="model-purposes-heading">
          <h2 id="model-purposes-heading">Modelo por finalidade</h2>
          <p className={styles.hint}>
            Cada tarefa pode usar um modelo diferente do provedor configurado. Um modelo maior costuma responder melhor,
            custa mais e demora mais. O teto de tokens de cada execução continua o mesmo, e nenhum agente escolhe o
            modelo: a escolha é sua.
          </p>
          <ul className={styles.purposes}>
            {connection.purposes.map((purpose) => (
              <PurposeRow
                key={purpose.id}
                purpose={purpose}
                connection={connection}
                busy={busy}
                onChoose={(model) => void choose(purpose.id, model, purpose.label)}
              />
            ))}
          </ul>
        </section>
      ) : null}
    </Page>
  );
}

function PurposeRow({
  purpose,
  connection,
  busy,
  onChoose
}: {
  purpose: ModelPurpose;
  connection: ModelConnection;
  busy: boolean;
  onChoose: (model: string | null) => void;
}) {
  const models = connection.available.find((entry) => entry.id === connection.provider)?.models ?? [];
  const defaultModel = connection.model;
  return (
    <li className={styles.purpose}>
      <SelectField
        label={purpose.label}
        size="small"
        fullWidth
        value={purpose.model}
        disabled={busy}
        onChange={(event) => onChoose(String(event.target.value) === defaultModel ? null : String(event.target.value))}
        options={models.map((option) => ({
          label: option.id === defaultModel ? `${option.name} (padrão)` : option.name,
          value: option.id
        }))}
      />
      <p className={styles.hint}>{purpose.description}</p>
    </li>
  );
}

function ConnectionState({ connection }: { connection: ModelConnection }) {
  const state = STATE[connection.status] ?? { label: connection.status, tone: 'neutral' as const };
  if (!connection.configured) return <p className={styles.status}>Nenhuma chave configurada.</p>;

  const protection = protectionText(connection.protection);
  return (
    <div className={styles.state}>
      <p className={styles.status}>
        Provedor configurado: {connection.providerName} <StatusChip tone={state.tone}>{state.label}</StatusChip>
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
