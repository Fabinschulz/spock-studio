import styles from '../infrastructure-page.module.scss';
import { sizeValue } from '../domain/blueprint';
import { ENGINE_LABELS, PROVIDER_FIELDS } from '../domain/provider-fields';
import type { GenerateFlow } from '../hooks/use-generate-flow';
import { FormChoice, FormFlag, FormText, storageHint } from './generate-fields';

/** Serviço, banco, filas, alarmes e state. O que cada nuvem aceita vem do motor; os nomes, da tabela de campos. */
export function ComponentsSection({ flow }: { flow: GenerateFlow }) {
  const { form } = flow.state;
  const { service, database } = flow.options;
  const fields = PROVIDER_FIELDS[form.provider];
  return (
    <section className={styles.consent} aria-label="Componentes">
      <p className={styles.rowTitle}>Componentes</p>
      <FormFlag flow={flow} field="withService" label={fields.service} />
      {form.withService ? (
        <>
          <FormText flow={flow} field={{ key: 'serviceName', label: 'Nome do serviço', hint: 'Até 10 caracteres.' }} />
          <FormText flow={flow} field={{ key: 'port', label: 'Porta do contêiner' }} />
          <FormText flow={flow} field={{ key: 'healthPath', label: 'Caminho do health check' }} />
          <FormChoice
            flow={flow}
            field="size"
            legend="CPU e memória"
            options={service.sizes.map((size) => ({
              value: sizeValue(size),
              label: `${size.cpu} CPU, ${size.memory} MB`
            }))}
          />
          <FormText flow={flow} field={{ key: 'desiredCount', label: fields.desiredCount }} />
          {service.certificate ? <FormText flow={flow} field={fields.certificate} /> : null}
          {service.publicIp ? (
            <FormFlag flow={flow} field="publicIp" label="Tarefas com IP público (subnets públicas, sem NAT)" />
          ) : null}
        </>
      ) : null}
      <FormFlag flow={flow} field="withDatabase" label={fields.database} />
      {form.withDatabase ? (
        <>
          <FormChoice
            flow={flow}
            field="engine"
            legend="Motor"
            options={database.engines.map((engine) => ({ value: engine, label: ENGINE_LABELS[engine] ?? engine }))}
          />
          <FormText flow={flow} field={{ key: 'engineVersion', label: 'Versão principal', hint: 'Ex.: 16.' }} />
          <FormText
            flow={flow}
            field={{ key: 'instanceClass', label: fields.instanceClass, hint: `Ex.: ${database.instanceClass}.` }}
          />
          <FormText
            flow={flow}
            field={{ key: 'storageGb', label: 'Armazenamento (GB)', hint: storageHint(database.storage) }}
          />
          <FormFlag flow={flow} field="multiAz" label={fields.multiAz} />
        </>
      ) : null}
      <FormText
        flow={flow}
        field={{
          key: 'queues',
          label: fields.queues,
          hint: 'Uma por fluxo, separadas por vírgula (ex.: pedidos, estoque).'
        }}
      />
      <FormText flow={flow} field={{ key: 'alarmEmail', label: 'E-mail dos alarmes (opcional)' }} />
      <FormText
        flow={flow}
        field={{
          key: 'bucket',
          label: fields.bucket,
          hint: 'O do bootstrap. Sem ele, a raiz nasce sem backend remoto.'
        }}
      />
      {fields.stateLocation && form.bucket.trim() ? (
        <>
          <FormText flow={flow} field={{ key: 'bucketResourceGroup', label: 'Grupo de recursos da storage account' }} />
          <FormText flow={flow} field={{ key: 'bucketContainer', label: 'Container do state' }} />
        </>
      ) : null}
    </section>
  );
}
