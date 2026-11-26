import { Page, PageSection, Term } from '@/shared/components';
import { app } from '@/shared/config/app';
import { useAsyncResource, useCopyToClipboard, useEngineStatus, type CopyState } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';
import { useCallback } from 'react';

import { DiagnosticLogSection } from './components/diagnostic-log-section';
import styles from './diagnostics-page.module.scss';
import { collapseRepeats } from './domain/diagnostic-log';
import { buildDiagnostics, formatDiagnostics } from './domain/diagnostics-report';
import { readDiagnosticLog } from './service/diagnostic-log-api';

// Fica ao lado do botão: quem clica olha para ali.
const COPY_FEEDBACK: Record<CopyState, string> = {
  idle: '',
  copied: 'Informações copiadas.',
  failed: 'Não foi possível copiar. Selecione o texto da tela e copie à mão.'
};

export function DiagnosticsPage() {
  const engine = useEngineStatus();
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 4000 });
  const readLog = useCallback(
    async () => (engine.hosted ? collapseRepeats(await readDiagnosticLog()) : []),
    [engine.hosted]
  );
  const log = useAsyncResource(readLog);
  const lines = buildDiagnostics({ appName: app.name, version: app.version, userAgent: navigator.userAgent, engine });
  const report = () => formatDiagnostics(lines, log.status === 'ready' ? log.value : []);

  return (
    <Page
      title="Diagnóstico"
      description="O que está rodando neste computador, para quem dá suporte. Não inclui chave, caminho de projeto nem conteúdo do que você faz."
    >
      <PageSection
        title="Este computador"
        description="O texto copiado traz as linhas desta tela e o registro recente."
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
          {lines.map(({ label, value, term }) => (
            <div key={label} className={styles.line}>
              <dt className={styles.label}>{term ? <Term id={term}>{label}</Term> : label}</dt>
              <dd className={styles.value}>{value}</dd>
            </div>
          ))}
        </dl>
      </PageSection>

      <DiagnosticLogSection log={log} hosted={engine.hosted} />
    </Page>
  );
}
