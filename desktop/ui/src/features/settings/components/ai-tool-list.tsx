import { StatusChip, Term } from '@/shared/components';
import type { AiTool } from '@/shared/schema';
import type { ReactNode } from 'react';

import { describeAiTool, type HarnessReach } from '../domain/ai-tool-labels';
import styles from './ai-tool-list.module.scss';

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

/** As ferramentas de linha de comando que o motor procurou, com o que o Spock faz com cada uma. */
export function AiToolList({ tools }: { tools: readonly AiTool[] }) {
  return (
    <ul className={styles.tools}>
      {tools.map((tool) => {
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
  );
}
