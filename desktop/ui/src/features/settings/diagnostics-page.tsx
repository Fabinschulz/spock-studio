import { Page, PageSection } from '@/shared/components';
import { app } from '@/shared/config/app';
import { useCopyToClipboard, useEngineStatus, type CopyState } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';

import styles from './diagnostics-page.module.scss';
import { buildDiagnostics, formatDiagnostics } from './domain/diagnostics-report';

const COPY_FEEDBACK: Record<CopyState, string> = {
  idle: 'O texto copiado traz as mesmas linhas desta tela.',
  copied: 'Informações copiadas.',
  failed: 'Não foi possível copiar. Selecione o texto da tela e copie à mão.'
};

export function DiagnosticsPage() {
  const engine = useEngineStatus();
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 4000 });
  const lines = buildDiagnostics({ appName: app.name, version: app.version, userAgent: navigator.userAgent, engine });

  return (
    <Page
      title="Diagnóstico"
      description="O que está rodando neste computador, para quem dá suporte. Não inclui chave, caminho de projeto nem conteúdo do que você faz."
    >
      <PageSection
        title="Este computador"
        action={
          <Button variant="outlined" onClick={() => void copy(formatDiagnostics(lines))}>
            Copiar informações
          </Button>
        }
      >
        <dl className={styles.lines}>
          {lines.map(({ label, value }) => (
            <div key={label} className={styles.line}>
              <dt className={styles.label}>{label}</dt>
              <dd className={styles.value}>{value}</dd>
            </div>
          ))}
        </dl>
        <p className={styles.hint} role="status">
          {COPY_FEEDBACK[state]}
        </p>
      </PageSection>
    </Page>
  );
}
