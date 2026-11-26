import type { EngineClient } from '@/shared/api';
import { Disclosure, ErrorState, InlineNotice, LoadingState, Page, PageSection, Term } from '@/shared/components';
import type { ProviderConnection } from '@/shared/schema';
import { useMemo, useState } from 'react';

import { BudgetsSection } from './components/budgets-section';
import { ConnectProvider } from './components/connect-provider';
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
  const [justConnected, setJustConnected] = useState<string | null>(null);

  const connected = connection?.providers.filter((provider) => provider.configured) ?? [];
  const missing = connection?.providers.filter((provider) => !provider.configured) ?? [];

  const connect = async (provider: ProviderConnection, key: string) => {
    const done = await save(provider.id, provider.name, key);
    if (done) setJustConnected(provider.id);
    return done;
  };

  const connectBlock = <ConnectProvider providers={missing} busy={busy} feedback={feedback} onConnect={connect} />;

  return (
    <Page
      title="Modelos de IA"
      description={
        <>
          O <Term id="provider">provedor</Term> e o <Term id="model">modelo de IA</Term> que respondem aos pedidos, e
          quanto cada execução pode gastar.
        </>
      }
    >
      {connection && connected.length === 0 ? (
        <InlineNotice tone="info" title="Nenhum modelo conectado">
          Sem ele, as leituras e verificações locais continuam funcionando. Com ele, o Spock Studio explica análises,
          responde perguntas e planeja pedidos. Basta um provedor.
        </InlineNotice>
      ) : null}

      <PageSection title="Provedores" description="Cada provedor tem a sua chave, guardada só neste computador.">
        {connection ? (
          <>
            {connected.length > 0 ? (
              <ul className={styles.providers}>
                {connected.map((provider) => (
                  <ProviderRow
                    key={provider.id}
                    provider={provider}
                    busy={busy}
                    feedback={feedback}
                    announce={provider.id === justConnected}
                    onSave={(entry, key) => save(entry.id, entry.name, key)}
                    onTest={(entry) => test(entry.id, entry.name)}
                    onSetDefault={(entry) => setDefault(entry.id, entry.name)}
                    onRemove={(entry) => remove(entry.id, entry.name)}
                  />
                ))}
              </ul>
            ) : (
              connectBlock
            )}
            {connected.length > 0 && missing.length > 0 ? (
              <Disclosure summary="Conectar outro provedor">{connectBlock}</Disclosure>
            ) : null}
            <Disclosure summary="Onde a chave fica e como ela é conferida">
              <p className={styles.note}>
                A <Term id="apiKey" /> fica só neste computador, fora do projeto. Ela nunca aparece no índice do
                projeto, nos registros das execuções nem no diagnóstico. Guardar a chave de um provedor não mexe nas dos
                outros, e ela é conferida com o provedor antes de ser guardada: se ele recusar, a atual continua
                valendo. O provedor padrão vale para tudo em que você não escolheu outro modelo.
              </p>
            </Disclosure>
          </>
        ) : loadError ? (
          <>
            <ErrorState onRetry={reload}>{loadError}</ErrorState>
            <RestartEngine onReady={reload} />
          </>
        ) : (
          <LoadingState>Lendo a configuração…</LoadingState>
        )}
      </PageSection>

      {connection ? (
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
