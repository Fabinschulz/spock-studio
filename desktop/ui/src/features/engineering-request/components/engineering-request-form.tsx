import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Button,
  CheckboxFormField,
  FormProvider,
  TextFormField,
  useFormContext
} from '@synthra.io/ui-kit';

import { pathTo } from '@/shared/routing';
import { useNavigate } from 'react-router';

import {
  engineeringRequestFormDefaults,
  engineeringRequestFormSchema,
  type EngineeringRequestFormValues
} from '../domain/form-schema';
import styles from '../engineering-request-page.module.scss';

interface EngineeringRequestFormProps {
  busy: boolean;
  error: string | null;
  onSubmit: (values: EngineeringRequestFormValues) => Promise<void>;
}

function FormActions({ busy }: { busy: boolean }) {
  const navigate = useNavigate();
  const { submitting, isValid } = useFormContext();

  const disabled = busy || submitting || !isValid;

  return (
    <div className={styles.actions}>
      <Button type="submit" disabled={disabled}>
        {busy || submitting ? 'Iniciando…' : 'Iniciar pedido'}
      </Button>
      <Button type="button" variant="outlined" color="inherit" onClick={() => navigate(pathTo('configure'))}>
        Configurar projeto
      </Button>
    </div>
  );
}

export function EngineeringRequestForm({ busy, error, onSubmit }: EngineeringRequestFormProps) {
  return (
    <FormProvider<EngineeringRequestFormValues>
      defaultValues={engineeringRequestFormDefaults}
      resolver={zodResolver(engineeringRequestFormSchema)}
      onSubmit={onSubmit}
    >
      <TextFormField
        name="goal"
        label="Objetivo"
        placeholder="Ex.: descrever impacto de alterar o contrato de autenticação"
        multiline
        minRows={4}
        fullWidth
        required
      />

      <div className={styles.options}>
        <CheckboxFormField name="impact" label="Incluir análise de impacto técnico" />
        <CheckboxFormField name="requiresApproval" label="Pausar para aprovação antes de concluir" />
      </div>

      {error ? <Alert severity="error" title="Não foi possível iniciar" description={error} /> : null}

      <FormActions busy={busy} />
    </FormProvider>
  );
}
