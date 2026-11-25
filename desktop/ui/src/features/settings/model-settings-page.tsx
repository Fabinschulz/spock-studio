import type { EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, Page, PageSection, SuccessState, Term } from '@/shared/components';
import { useMemo } from 'react';

import { AiToolsSection } from './components/ai-tools-section';
import { BudgetsSection } from './components/budgets-section';
import { ProviderRow } from './components/provider-row';
import { PurposesSection } from './components/purposes-section';
import { useModelSettings } from './hooks/use-model-settings';
import styles from './model-settings-page.module.scss';
import { createModelApi } from './service/model-api';

interface ModelSettingsPageProps {
  client: EngineClient;
  /** O nome que o menu de onde a pessoa veio dá à tela. */
  title?: string;
}

export function ModelSettingsPage({ client, title = 'Minha IA' }: ModelSettingsPageProps) {
  const api = useMemo(() => createModelApi(client), [client]);
  const { connection, busy, error, notice, save, test, choose, setBudget, remove, setDefault } = useModelSettings(api);

  return (
    <Page
      title={title}
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
                onSave={(entry, key) => save(entry.id, entry.name, key)}
                onTest={(entry) => void test(entry.id, entry.name)}
                onSetDefault={(entry) => void setDefault(entry.id, entry.name)}
                onRemove={(entry) => void remove(entry.id, entry.name)}
              />
            ))}
          </ul>
        ) : (
          <LoadingState>Lendo a configuração…</LoadingState>
        )}
        {error ? <ErrorState>{error}</ErrorState> : null}
        {notice ? <SuccessState>{notice}</SuccessState> : null}
      </PageSection>

      {connection?.configured ? (
        <PurposesSection
          connection={connection}
          busy={busy}
          onChoose={(purpose, model) => void choose(purpose.id, model, purpose.label)}
        />
      ) : null}

      {connection ? (
        <BudgetsSection
          connection={connection}
          busy={busy}
          onSave={(budget, tokens) => void setBudget(budget.purpose, tokens, budget.label)}
        />
      ) : null}

      <AiToolsSection client={client} />
    </Page>
  );
}
