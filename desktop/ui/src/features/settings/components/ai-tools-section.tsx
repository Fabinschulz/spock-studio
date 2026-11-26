import type { EngineClient } from '@/shared/api';
import { ErrorState, LoadingState, PageSection, StatusChip, Term } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';
import { useMemo, type ReactNode } from 'react';

import { describeAiTool, type HarnessReach } from '../domain/ai-tool-labels';
import { describeFailure } from '../domain/model-errors';
import { createAiToolsApi } from '../service/ai-tools-api';
import styles from './ai-tools-section.module.scss';

const REACH: Record<HarnessReach, ReactNode> = {
  full: (
    <>
      O Spock configura o <Term id="harness" /> para esta ferramenta.
    </>
  ),
  partial: (
    <>
      O Spock liga esta ferramenta ao projeto: uma regra, a conexão <Term id="mcp" />, os <Term id="hook">hooks</Term> e
      a <Term id="skill" /> do contrato. <Term id="agent">Agentes</Term> e regras ainda são só do Claude Code.
    </>
  ),
  none: (
    <>
      O Spock detecta esta ferramenta, mas ainda não configura o <Term id="harness" /> dela.
    </>
  )
};

export function AiToolsSection({ client }: { client: EngineClient }) {
  const api = useMemo(() => createAiToolsApi(client), [client]);
  const tools = useAsyncResource(api.list);

  return (
    <PageSection
      title="Ferramentas de IA neste computador"
      description="O Spock procura estas ferramentas de linha de comando. Ele não confere o login de nenhuma delas."
      action={
        <Button variant="outlined" onClick={tools.reload}>
          Verificar de novo
        </Button>
      }
    >
      {tools.status === 'loading' ? (
        <LoadingState>Procurando as ferramentas…</LoadingState>
      ) : tools.status === 'error' ? (
        <ErrorState onRetry={tools.reload}>
          {describeFailure(tools.error, 'Não foi possível procurar as ferramentas.')}
        </ErrorState>
      ) : (
        <ul className={styles.tools}>
          {tools.value.map((tool) => {
            const { state, tone, detail, harness } = describeAiTool(tool);
            return (
              <li key={tool.id} className={styles.tool}>
                <p className={styles.name}>
                  {tool.name} <StatusChip tone={tone}>{state}</StatusChip>
                </p>
                <p className={styles.hint}>{detail}</p>
                <p className={styles.hint}>{REACH[harness]}</p>
              </li>
            );
          })}
        </ul>
      )}
    </PageSection>
  );
}
