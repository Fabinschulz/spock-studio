import type { EngineClient } from '@/shared/api';
import { Disclosure, Page, PageSection } from '@/shared/components';
import { app } from '@/shared/config/app';
import { useAsyncResource, useCopyToClipboard, useEngineStatus, type CopyState } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';

import { DiagnosticChecksSection } from './components/diagnostic-checks-section';
import { DiagnosticLogSection } from './components/diagnostic-log-section';
import styles from './diagnostics-page.module.scss';
import { buildChecks } from './domain/diagnostic-checks';
import { collapseRepeats } from './domain/diagnostic-log';
import { buildDiagnostics, formatDiagnostics } from './domain/diagnostics-report';
import { createAiToolsApi } from './service/ai-tools-api';
import { readDiagnosticLog } from './service/diagnostic-log-api';
import { createModelApi } from './service/model-api';

// Fica ao lado do botão: quem clica olha para ali.
const COPY_FEEDBACK: Record<CopyState, string> = {
  idle: '',
  copied: 'Informações copiadas.',
  failed: 'Não foi possível copiar. Selecione o texto da tela e copie à mão.'
};

const NOT_READY = () => Promise.reject(new Error('O motor não está pronto.'));

export function DiagnosticsPage({ client }: { client: EngineClient }) {
  const engine = useEngineStatus();
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 4000 });
  const modelApi = useMemo(() => createModelApi(client), [client]);
  const toolsApi = useMemo(() => createAiToolsApi(client), [client]);

  // Só pergunta ao motor quando ele está pronto, e pergunta de novo quando ele fica pronto.
  const engineReady = engine.hosted && engine.status === 'ready';
  const readConnection = useCallback(
    () => (engineReady ? modelApi.connection() : NOT_READY()),
    [engineReady, modelApi]
  );
  const readTools = useCallback(() => (engineReady ? toolsApi.list() : NOT_READY()), [engineReady, toolsApi]);
  const readLog = useCallback(
    async () => (engine.hosted ? collapseRepeats(await readDiagnosticLog()) : []),
    [engine.hosted]
  );
  const connection = useAsyncResource(readConnection);
  const tools = useAsyncResource(readTools);
  const log = useAsyncResource(readLog);

  const checks = buildChecks({ engine, connection, tools });
  const lines = buildDiagnostics({ appName: app.name, version: app.version, userAgent: navigator.userAgent });
  const [version, browser] = lines;
  const report = () => formatDiagnostics({ checks, lines, log: log.status === 'ready' ? log.value : [] });
  const recheck = () => {
    connection.reload();
    tools.reload();
    log.reload();
  };

  return (
    <Page
      title="Diagnóstico"
      description="Se o Spock Studio está pronto neste computador e, quando algo não está, o que fazer. Para o suporte, copie as informações."
    >
      <DiagnosticChecksSection
        checks={checks}
        tools={tools.status === 'ready' ? tools.value : null}
        onRecheck={recheck}
      />

      <PageSection
        title="Para o suporte"
        description="O texto copiado traz as verificações, a versão e o registro recente. Não inclui chave, caminho de projeto nem conteúdo do que você faz."
        action={
          <div className={styles.copy}>
            <p className={styles.hint} role="status">
              {COPY_FEEDBACK[state]}
            </p>
            <Button variant="outlined" onClick={() => void copy(report())}>
              Copiar informações
            </Button>
          </div>
        }
      >
        <dl className={styles.lines}>
          <div className={styles.line}>
            <dt className={styles.label}>{version.label}</dt>
            <dd className={styles.value}>{version.value}</dd>
          </div>
        </dl>
        <Disclosure summary={browser.label}>
          <p className={styles.value}>{browser.value}</p>
        </Disclosure>
      </PageSection>

      <DiagnosticLogSection log={log} hosted={engine.hosted} />
    </Page>
  );
}
