import { Disclosure } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { ProjectInvestigation } from '@/shared/schema';
import { Link } from 'react-router';

import styles from '../management-page.module.scss';

/** Não mostra texto de hipótese nem a pergunta: o motor não os guarda. */
export function InvestigationPath({ investigation }: { investigation: ProjectInvestigation }) {
  const rounds = investigation.rounds;
  return (
    <section aria-label="Caminho da investigação" className={styles.answerGroup}>
      <Disclosure
        summary={
          <>
            Caminho da investigação: {rounds} {rounds === 1 ? 'rodada' : 'rodadas'}, {investigation.path.length} etapas
            {investigation.usedModel ? '' : ', sem modelo'}
          </>
        }
      >
        <ol className={styles.path}>
          {investigation.path.map((step, index) => (
            <li key={`${step.id}-${index}`}>
              <strong>{step.title}</strong>
              {step.detail ? <span> {step.detail}</span> : null}
              {step.facts.length > 0 ? <p className={styles.hint}>Fatos: {step.facts.join(', ')}.</p> : null}
            </li>
          ))}
        </ol>
        {investigation.limitations.length > 0 ? (
          <ul className={styles.notes} aria-label="O que limitou a investigação">
            {investigation.limitations.map((limitation) => (
              <li key={limitation.code}>{limitation.message}</li>
            ))}
          </ul>
        ) : null}
        <p className={styles.hint}>
          Esta investigação ficou registrada como <span>{investigation.runId}</span> na{' '}
          <Link to={pathTo('runs')}>central de execuções</Link>, com o mesmo caminho.
        </p>
      </Disclosure>
    </section>
  );
}
