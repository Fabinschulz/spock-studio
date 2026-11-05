import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, CheckboxFormField, FormProvider, TextFormField, useFormContext } from '@mariliz/ui-kit';

import { pathTo } from '@/shared/routing';
import { useId } from 'react';
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
  /** Há modelo configurado? `null` até o motor responder. */
  modelConfigured: boolean | null;
  onSubmit: (values: EngineeringRequestFormValues) => Promise<void>;
}

/** O que a pessoa precisa saber antes de marcar: o que sai do computador, para onde, e o que o motor faz para limitar. */
function ModelOption({ configured }: { configured: boolean | null }) {
  const navigate = useNavigate();
  const descriptionId = useId();

  return (
    <div className={styles.model}>
      <CheckboxFormField
        name="useModel"
        label="Usar o modelo de IA no planejamento e na análise"
        disabled={configured !== true}
        slotProps={{ input: { 'aria-describedby': descriptionId } }}
      />
      <p id={descriptionId} className={styles.note}>
        Se marcado, o pedido e os trechos do projeto recuperados para ele são enviados à OpenAI, com segredos removidos
        antes. O gasto é limitado a 8.000 tokens e o motor o contabiliza. Sem marcar, nada sai do seu computador.
      </p>
      {configured === false ? (
        <p className={styles.note}>
          Nenhum modelo está configurado.{' '}
          <Button type="button" variant="text" onClick={() => navigate(pathTo('settings'))}>
            Guardar a chave em Configurações
          </Button>
        </p>
      ) : null}
    </div>
  );
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

export function EngineeringRequestForm({ busy, error, modelConfigured, onSubmit }: EngineeringRequestFormProps) {
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
        <ModelOption configured={modelConfigured} />
      </div>

      {error ? <Alert severity="error" title="Não foi possível iniciar" description={error} /> : null}

      <FormActions busy={busy} />
    </FormProvider>
  );
}
