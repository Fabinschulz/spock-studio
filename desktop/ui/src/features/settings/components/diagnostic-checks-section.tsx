import { Disclosure, PageSection, StatusChip, Term } from '@/shared/components';
import type { AiTool } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import { CHECK_STATE, type DiagnosticCheck } from '../domain/diagnostic-checks';
import { AiToolList } from './ai-tool-list';
import { RestartEngine } from './restart-engine';
import styles from './diagnostic-checks-section.module.scss';

interface DiagnosticChecksSectionProps {
  checks: readonly DiagnosticCheck[];
  /** A lista que a verificação de ferramentas resume, quando o motor respondeu. */
  tools: readonly AiTool[] | null;
  onRecheck: () => void;
}

export function DiagnosticChecksSection({ checks, tools, onRecheck }: DiagnosticChecksSectionProps) {
  return (
    <PageSection
      title="Verificações"
      description="O que o Spock Studio confere neste computador e o que fazer quando algo não está certo."
      action={
        <Button variant="outlined" onClick={onRecheck}>
          Verificar de novo
        </Button>
      }
    >
      <ul className={styles.checks}>
        {checks.map((check) => {
          const state = CHECK_STATE[check.state];
          return (
            <li key={check.id} className={styles.check}>
              <p className={styles.header}>
                <span className={styles.title}>
                  {check.term ? <Term id={check.term}>{check.title}</Term> : check.title}
                </span>
                <StatusChip tone={state.tone}>{state.label}</StatusChip>
              </p>
              <p className={styles.summary}>{check.summary}</p>
              {check.action ? <Link to={check.action.to}>{check.action.label}</Link> : null}
              {check.id === 'engine' ? <RestartEngine /> : null}
              {check.id === 'aiTools' && tools && tools.length > 0 ? (
                <Disclosure summary="Ver cada ferramenta">
                  <p className={styles.summary}>
                    O Spock procura estas ferramentas de linha de comando. Ele não confere o login de nenhuma delas.
                  </p>
                  <AiToolList tools={tools} />
                </Disclosure>
              ) : null}
            </li>
          );
        })}
      </ul>
    </PageSection>
  );
}
