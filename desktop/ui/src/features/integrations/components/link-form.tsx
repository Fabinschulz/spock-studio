import type { Connection, Connector } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useState, type FormEvent } from 'react';

import styles from '../integrations-page.module.scss';

interface LinkFormProps {
  connection: Connection;
  connector: Connector;
  busy: boolean;
  onSubmit: (resource: string, dataKinds: string[]) => void;
  onCancel: () => void;
}

export function LinkForm({ connection, connector, busy, onSubmit, onCancel }: LinkFormProps) {
  const [resource, setResource] = useState('');
  const [kinds, setKinds] = useState<string[]>(connector.dataKinds.map((kind) => kind.id));
  const complete = resource.trim().length > 0 && kinds.length > 0;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (complete) onSubmit(resource.trim(), kinds);
  };

  return (
    <form className={styles.form} aria-label={`Vincular ${connection.name} a este projeto`} onSubmit={submit}>
      <TextField
        label={connector.resource.label}
        fullWidth
        value={resource}
        disabled={busy}
        onChange={(event) => setResource(event.target.value)}
        helperText={connector.resource.hint}
        slotProps={{ htmlInput: { spellCheck: false } }}
      />
      <fieldset className={styles.kinds}>
        <legend>Dados que este projeto pode ler</legend>
        {connector.dataKinds.map((kind) => (
          <Checkbox
            key={kind.id}
            label={kind.label}
            checked={kinds.includes(kind.id)}
            disabled={busy}
            onChange={(_, checked) =>
              setKinds((current) => (checked ? [...current, kind.id] : current.filter((id) => id !== kind.id)))
            }
          />
        ))}
      </fieldset>
      {complete ? null : (
        <p className={styles.hint}>Para vincular, informe o recurso e marque pelo menos um tipo de dado.</p>
      )}
      <div className={styles.actions}>
        <Button type="submit" variant="contained" disabled={busy || !complete}>
          Vincular
        </Button>
        <Button type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
