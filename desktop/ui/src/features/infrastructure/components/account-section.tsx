import { Radio } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { CloudConnection } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import styles from '../infrastructure-page.module.scss';
import { LINKED_UNREADABLE } from '../domain/linked-connections';
import { PROVIDER_FIELDS } from '../domain/provider-fields';
import type { GenerateFlow } from '../hooks/use-generate-flow';
import { FormText } from './generate-fields';

interface AccountSectionProps {
  flow: GenerateFlow;
  connection: CloudConnection | null;
  linked: CloudConnection[] | null;
}

/** Conta e rede existente: a conexão vinculada preenche o que tem, e o resto a pessoa digita. Nada disso vai ao modelo. */
export function AccountSection({ flow, connection, linked }: AccountSectionProps) {
  const { state, options, busy } = flow;
  const { form, vpcs } = state;
  const fields = PROVIDER_FIELDS[form.provider];
  return (
    <section className={styles.consent} aria-label="Conta e rede">
      <p className={styles.rowTitle}>Conta e rede existente</p>
      {connection ? (
        <div className={styles.actions}>
          <Button
            type="button"
            variant="text"
            disabled={busy}
            onClick={() => flow.dispatch({ type: 'connection', connection })}
          >
            Preencher conta e região pela conexão {connection.name}
          </Button>
          {fields.subnetDiscovery ? (
            <Button
              type="button"
              variant="text"
              disabled={busy || form.region.trim().length === 0}
              onClick={() => void flow.readVpcs(connection, fields.subnetDiscovery!)}
            >
              Ler VPCs e subnets da conta
            </Button>
          ) : null}
        </div>
      ) : linked === null ? (
        <p className={styles.hint}>{LINKED_UNREADABLE}</p>
      ) : (
        <p className={styles.hint}>
          Vincule uma conexão {options.label} ao projeto em <Link to={pathTo('cloud')}>Conexões › Nuvem</Link> para
          preencher conta e região sem digitar.
        </p>
      )}
      {vpcs ? (
        vpcs.length === 0 ? (
          <p className={styles.hint}>Nenhuma subnet nesta região.</p>
        ) : (
          <fieldset className={styles.choices}>
            <legend>VPC encontrada</legend>
            {vpcs.map((vpc) => (
              <Radio
                key={vpc.id}
                name="iac-generate-vpc"
                value={vpc.id}
                checked={form.vpcId === vpc.id}
                disabled={busy}
                onChange={() => flow.dispatch({ type: 'vpc', vpc })}
                label={`${vpc.id} · ${vpc.privateSubnets.length} privada(s), ${vpc.publicSubnets.length} pública(s)`}
              />
            ))}
          </fieldset>
        )
      ) : null}
      <FormText
        flow={flow}
        field={{
          key: 'project',
          label: 'Nome do projeto',
          hint: 'Minúsculas, dígitos e hífen, até 18 caracteres (ex.: loja-api).'
        }}
      />
      <FormText flow={flow} field={{ key: 'region', label: 'Região', hint: `Ex.: ${options.regionExample}.` }} />
      {fields.account.map((field) => (
        <FormText key={field.key} flow={flow} field={field} />
      ))}
      {form.withDatabase
        ? fields.databaseNetwork.map((field) => <FormText key={field.key} flow={flow} field={field} />)
        : null}
    </section>
  );
}
