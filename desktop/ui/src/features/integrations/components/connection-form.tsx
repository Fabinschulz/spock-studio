import type { Connector } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState, type FormEvent } from 'react';

import styles from '../integrations-page.module.scss';
import type { NewConnection } from '../service/integration-api';

interface ConnectionFormProps {
  connector: Connector;
  busy: boolean;
  onSubmit: (input: NewConnection) => void;
  onCancel: () => void;
}

/** Os campos vêm do conector: a interface não sabe o que cada provedor pede. A credencial só existe neste estado, até o envio. */
export function ConnectionForm({ connector, busy, onSubmit, onCancel }: ConnectionFormProps) {
  const [name, setName] = useState(connector.name);
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [secret, setSecret] = useState('');

  const complete =
    name.trim().length > 0 &&
    secret.trim().length >= 8 &&
    connector.settings.every((field) => (settings[field.name] ?? '').trim().length > 0);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (complete) onSubmit({ provider: connector.provider, name: name.trim(), settings, secret: secret.trim() });
  };

  return (
    <form className={styles.form} aria-label={`Nova conexão com ${connector.name}`} onSubmit={submit}>
      <TextField
        label="Nome da conexão"
        fullWidth
        value={name}
        disabled={busy}
        onChange={(event) => setName(event.target.value)}
        helperText="Como ela aparece para quem for vincular projetos."
      />
      {connector.settings.map((field) => (
        <TextField
          key={field.name}
          label={field.label}
          fullWidth
          value={settings[field.name] ?? ''}
          disabled={busy}
          onChange={(event) => setSettings((current) => ({ ...current, [field.name]: event.target.value }))}
          helperText={field.hint}
          slotProps={{ htmlInput: { spellCheck: false } }}
        />
      ))}
      <TextField
        label={connector.secret.label}
        type="password"
        autoComplete="off"
        fullWidth
        value={secret}
        disabled={busy}
        onChange={(event) => setSecret(event.target.value)}
        helperText={connector.secret.hint}
        slotProps={{ htmlInput: { spellCheck: false } }}
      />
      <p className={styles.hint}>{connector.scopesNote}</p>
      <div className={styles.actions}>
        <Button type="submit" variant="contained" disabled={busy || !complete}>
          Conectar
        </Button>
        <Button type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
