import { Disclosure } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { EvidencePack, ProjectInvestigation } from '@/shared/schema';
import { plural } from '@/shared/utils';
import { Link } from 'react-router';

import styles from '../management-page.module.scss';

/**
 * Não mostra texto de hipótese nem a pergunta: o motor não os guarda. O caminho só traz ids de fato; o texto vem dos fatos
 * que a tela já tem (os do projeto e os que a resposta cita), e um id sem texto conta no número, mas não aparece cru.
 */
export function InvestigationPath({
  investigation,
  known
}: {
  investigation: ProjectInvestigation;
  known: EvidencePack['facts'];
}) {
  const rounds = investigation.rounds;
  const facts = new Map([...known, ...investigation.answer.evidence].map((fact) => [fact.id, fact.text]));

  return (
    <section aria-label="Caminho da investigação" className={styles.answerGroup}>
      <Disclosure
        summary={
          <>
            Caminho da investigação: {plural(rounds, 'rodada', 'rodadas')}, {investigation.path.length} etapas
            {investigation.usedModel ? '' : ', sem modelo'}
          </>
        }
      >
        <ol className={styles.path}>
          {investigation.path.map((step, index) => (
            <li key={`${step.id}-${index}`}>
              <strong>{step.title}</strong>
              {step.detail ? <span> {step.detail}</span> : null}
              <StepFacts ids={step.facts} facts={facts} />
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
          Esta investigação ficou guardada em <Link to={pathTo('runs')}>Execuções</Link>, com o mesmo caminho.
        </p>
      </Disclosure>
    </section>
  );
}

function StepFacts({ ids, facts }: { ids: string[]; facts: Map<string, string> }) {
  if (ids.length === 0) return null;
  const texts = ids.map((id) => facts.get(id)).filter((text): text is string => text !== undefined);
  if (texts.length === 0) return <p className={styles.hint}>{plural(ids.length, 'fato reunido', 'fatos reunidos')}.</p>;

  return (
    <ul className={styles.hint} aria-label="Fatos usados nesta etapa">
      {texts.map((text) => (
        <li key={text}>{text}</li>
      ))}
      {texts.length < ids.length ? <li>e {plural(ids.length - texts.length, 'outro fato', 'outros fatos')}</li> : null}
    </ul>
  );
}
