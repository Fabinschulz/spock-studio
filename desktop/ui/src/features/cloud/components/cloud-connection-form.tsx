import { Radio } from '@/shared/components';
import type { CloudProvider } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState, type FormEvent } from 'react';

import styles from '../cloud-page.module.scss';
import type { NewCloudConnection } from '../service/cloud-api';

interface CloudConnectionFormProps {
  provider: CloudProvider;
  busy: boolean;
  onSubmit: (input: NewCloudConnection) => void;
  onCancel: () => void;
}

/** Os campos mudam com o mecanismo e vêm do motor. Não há campo de segredo: a credencial fica onde o SDK a procura. */
export function CloudConnectionForm({ provider, busy, onSubmit, onCancel }: CloudConnectionFormProps) {
  const [name, setName] = useState(provider.name);
  const [mechanismId, setMechanismId] = useState(provider.mechanisms[0]?.id ?? '');
  const [values, setValues] = useState<Record<string, string>>({});
  const mechanism = provider.mechanisms.find((candidate) => candidate.id === mechanismId);
  const fields = mechanism?.fields ?? [];
  const complete =
    name.trim().length > 0 &&
    mechanism !== undefined &&
    fields.every((field) => !field.required || (values[field.name] ?? '').trim().length > 0);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!complete) return;
    const settings: Record<string, string> = {};
    for (const field of fields) {
      const value = (values[field.name] ?? '').trim();
      if (value.length > 0) settings[field.name] = value;
    }
    onSubmit({ provider: provider.provider, name: name.trim(), mechanism: mechanismId, settings });
  };

  return (
    <form className={styles.form} aria-label={`Nova conexão com ${provider.name}`} onSubmit={submit}>
      <TextField
        label="Nome da conexão"
        fullWidth
        value={name}
        disabled={busy}
        onChange={(event) => setName(event.target.value)}
        helperText="Como ela aparece para quem for vincular projetos."
      />
      <fieldset className={styles.choices}>
        <legend>Como autenticar</legend>
        {provider.mechanisms.map((option) => (
          <Radio
            key={option.id}
            name={`mechanism-${provider.provider}`}
            value={option.id}
            checked={mechanismId === option.id}
            disabled={busy}
            onChange={() => setMechanismId(option.id)}
            label={option.label}
          />
        ))}
      </fieldset>
      {mechanism ? <p className={styles.hint}>{mechanism.description}</p> : null}
      {fields.map((field) => (
        <TextField
          key={`${mechanismId}-${field.name}`}
          label={field.required ? field.label : `${field.label} (opcional)`}
          fullWidth
          value={values[field.name] ?? ''}
          disabled={busy}
          onChange={(event) => setValues((current) => ({ ...current, [field.name]: event.target.value }))}
          helperText={field.hint}
          slotProps={{ htmlInput: { spellCheck: false } }}
        />
      ))}
      <p className={styles.hint}>
        O Spock Studio guarda só estes nomes. A credencial continua onde o SDK oficial do provedor a procura, e nada é
        criado nem alterado na conta: as leituras são só de consulta.
      </p>
      <div className={styles.actions}>
        <Button type="submit" variant="contained" disabled={busy || !complete}>
          Conectar e validar
        </Button>
        <Button type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
