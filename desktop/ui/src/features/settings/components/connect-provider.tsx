import { Radio } from '@/shared/components';
import type { ProviderConnection } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useId, useState } from 'react';

import { keySource, readKey } from '../domain/provider-labels';
import type { Feedback } from '../hooks/use-model-settings';
import { useRefocus } from '../hooks/use-refocus';
import { ActionFeedback } from './action-feedback';
import styles from './connect-provider.module.scss';

interface ConnectProviderProps {
  /** Só os provedores que ainda não têm chave. */
  providers: readonly ProviderConnection[];
  busy: boolean;
  feedback: Feedback | null;
  onConnect: (provider: ProviderConnection, key: string) => Promise<boolean>;
}

/** Um provedor por vez: escolher, colar a chave e deixar o provedor conferir antes de guardar. */
export function ConnectProvider({ providers, busy, feedback, onConnect }: ConnectProviderProps) {
  const groupName = useId();
  const [chosenId, setChosenId] = useState<string | null>(null);
  const [text, setText] = useState('');
  const { target: field, after } = useRefocus<HTMLInputElement>(busy);
  const chosen = providers.find((provider) => provider.id === chosenId) ?? providers[0];
  if (!chosen) return null;
  const input = readKey(text);

  const connect = async () => {
    if (input.ready && (await after(onConnect(chosen, input.key)))) setText('');
  };

  return (
    <div className={styles.connect}>
      <fieldset className={styles.choice} disabled={busy}>
        <legend>Qual provedor você usa?</legend>
        {providers.map((provider) => (
          <Radio
            key={provider.id}
            name={groupName}
            value={provider.id}
            checked={provider.id === chosen.id}
            onChange={() => setChosenId(provider.id)}
            label={provider.name}
          />
        ))}
      </fieldset>
      <p className={styles.hint}>
        {keySource(chosen.id)} A chave é como uma senha da sua conta, e o provedor pode cobrar pelo uso.
      </p>
      <TextField
        inputRef={field}
        label={`Chave de API (${chosen.name})`}
        type="password"
        autoComplete="off"
        fullWidth
        value={text}
        disabled={busy}
        error={input.malformed}
        onChange={(event) => setText(event.target.value)}
        helperText={input.helper}
        slotProps={{ htmlInput: { spellCheck: false } }}
      />
      <div className={styles.actions}>
        <Button
          type="button"
          disabled={busy || !input.ready}
          aria-label={`Conferir e guardar a chave de ${chosen.name}`}
          onClick={() => void connect()}
        >
          Conferir e guardar
        </Button>
        <p className={styles.hint}>
          {input.ready
            ? `O Spock Studio confere a chave com ${chosen.name} antes de guardar.`
            : 'Cole a chave para continuar.'}
        </p>
      </div>
      <ActionFeedback feedback={feedback} target={`provider:${chosen.id}`} />
    </div>
  );
}
