import type { EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Page, PageSection, Term } from '@/shared/components';
import { useMemo } from 'react';

import { BudgetsSection } from './components/budgets-section';
import { ProviderRow } from './components/provider-row';
import { RestartEngine } from './components/restart-engine';
import { PurposesSection } from './components/purposes-section';
import { useModelSettings } from './hooks/use-model-settings';
import styles from './model-settings-page.module.scss';
import { createModelApi } from './service/model-api';

export function ModelSettingsPage({ client }: { client: EngineClient }) {
  const api = useMemo(() => createModelApi(client), [client]);
  const { connection, busy, loadError, reload, feedback, save, test, choose, setBudget, remove, setDefault } =
    useModelSettings(api);

  return (
    <Page
      title="Modelos de IA"
      description={
        <>
          Os <Term id="provider">provedores</Term> e os <Term id="model">modelos</Term> que o Spock Studio usa. Cada
          provedor tem a sua chave, guardada só neste computador.
        </>
      }
    >
      <PageSection
        title="Provedores"
        description={
          <>
            A <Term id="apiKey" /> fica só neste computador, fora do projeto. Ela nunca aparece no índice do projeto,
            nos registros das execuções nem no diagnóstico. Guardar a chave de um provedor não mexe nas dos outros, e
            ela é conferida com o provedor antes de ser guardada: se ele recusar, a atual continua valendo. O provedor
            padrão vale para tudo em que você não escolheu outro modelo.
          </>
        }
      >
        {connection ? (
          <ul className={styles.providers}>
            {connection.providers.map((provider) => (
              <ProviderRow
                key={provider.id}
                provider={provider}
                busy={busy}
                feedback={feedback}
                onSave={(entry, key) => save(entry.id, entry.name, key)}
                onTest={(entry) => test(entry.id, entry.name)}
                onSetDefault={(entry) => setDefault(entry.id, entry.name)}
                onRemove={(entry) => remove(entry.id, entry.name)}
              />
            ))}
          </ul>
        ) : loadError ? (
          <>
            <ErrorState onRetry={reload}>{loadError}</ErrorState>
            <RestartEngine onReady={reload} />
          </>
        ) : (
          <LoadingState>Lendo a configuração…</LoadingState>
        )}
      </PageSection>

      {connection?.configured ? (
        <PurposesSection
          connection={connection}
          busy={busy}
          feedback={feedback}
          onChoose={(purpose, model) => choose(purpose.id, model, purpose.label)}
        />
      ) : null}

      {connection ? (
        <BudgetsSection
          connection={connection}
          busy={busy}
          feedback={feedback}
          onSave={(budget, tokens) => setBudget(budget.purpose, tokens, budget.label)}
        />
      ) : null}
    </Page>
  );
}
