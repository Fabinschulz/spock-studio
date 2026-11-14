import type { EngineClient } from '@/shared/api';
import { PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { Button } from '@mariliz/ui-kit';
import { useMemo } from 'react';

import { describeAiTool } from '../domain/ai-tool-labels';
import { createAiToolsApi } from '../service/ai-tools-api';
import styles from './ai-tools-section.module.scss';

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
      <ResourceView
        resource={tools}
        loading="Procurando as ferramentas…"
        fallbackError="Não foi possível procurar as ferramentas."
      >
        {(list) => (
          <ul className={styles.tools}>
            {list.map((tool) => {
              const { state, tone, detail, harness } = describeAiTool(tool);
              return (
                <li key={tool.id} className={styles.tool}>
                  <p className={styles.name}>
                    {tool.name} <StatusChip tone={tone}>{state}</StatusChip>
                  </p>
                  <p className={styles.hint}>{detail}</p>
                  <p className={styles.hint}>{harness}</p>
                </li>
              );
            })}
          </ul>
        )}
      </ResourceView>
    </PageSection>
  );
}
