import type { AssistantAnswer, AssistantStatement } from '@/shared/schema';
import { useEffect, useId, useRef } from 'react';

import { ANSWER_SECTIONS } from '../domain/assistant';
import { Disclosure } from '@/shared/components';
import styles from '../management-page.module.scss';

/**
 * O que a verificação descartou continua visível, com o motivo, para que nada suma sem explicação. A resposta nova leva o
 * foco ao título: quem usa leitor de tela fica sabendo que ela chegou, e a leitura começa por ela.
 */
export function AssistantAnswerView({ answer }: { answer: AssistantAnswer }) {
  const facts = new Map(answer.evidence.map((fact) => [fact.id, fact.text]));
  const titleId = useId();
  const title = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    title.current?.focus();
  }, [answer]);

  return (
    <section aria-labelledby={titleId} className={styles.answer}>
      <h2 id={titleId} ref={title} tabIndex={-1} className={styles.section}>
        Resposta
      </h2>
      {answer.summary ? <p className={styles.summary}>{answer.summary}</p> : null}

      {ANSWER_SECTIONS.map((section) =>
        answer[section.key].length > 0 ? (
          <AnswerSection
            key={section.key}
            label={section.label}
            hint={section.hint}
            statements={answer[section.key]}
            facts={facts}
          />
        ) : null
      )}

      {answer.unavailable.length > 0 ? (
        <div className={styles.answerGroup}>
          <h3 className={styles.kindTitle}>Informações indisponíveis</h3>
          <ul className={styles.notes}>
            {answer.unavailable.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {answer.rejected.length > 0 ? (
        <Disclosure
          className={styles.method}
          summary={
            <>
              {answer.rejected.length === 1
                ? '1 afirmação descartada pela verificação'
                : `${answer.rejected.length} afirmações descartadas pela verificação`}
            </>
          }
        >
          <ul>
            {answer.rejected.map((item) => (
              <li key={`${item.section}:${item.text}`}>
                <span className={styles.kindLabel}>{item.section}:</span> {item.text} <em>({item.reason})</em>
              </li>
            ))}
          </ul>
        </Disclosure>
      ) : null}

      <p className={styles.ruleNote}>
        {answer.model
          ? `Escrito por ${answer.model} a partir dos fatos do projeto e conferido pelo Spock Studio: cada afirmação cita os fatos que usou, e número que não está neles é descartado.`
          : 'Montado pelo Spock Studio só com os fatos do projeto: nenhum modelo de IA escreveu esta resposta.'}
      </p>
    </section>
  );
}

interface AnswerSectionProps {
  label: string;
  hint: string;
  statements: AssistantStatement[];
  facts: Map<string, string>;
}

function AnswerSection({ label, hint, statements, facts }: AnswerSectionProps) {
  const titleId = useId();

  return (
    <div className={styles.answerGroup} role="group" aria-labelledby={titleId}>
      <h3 id={titleId} className={styles.kindTitle}>
        {label}
        <span className={styles.sectionHint}>{hint}</span>
      </h3>
      <ul className={styles.statements}>
        {statements.map((statement) => (
          <li key={statement.text}>
            {statement.text}
            <Disclosure
              className={styles.sources}
              summary={
                <>{statement.evidence.length === 1 ? '1 fato citado' : `${statement.evidence.length} fatos citados`}</>
              }
            >
              <ul>
                {statement.evidence.map((id) => (
                  <li key={id}>{facts.get(id) ?? id}</li>
                ))}
              </ul>
            </Disclosure>
          </li>
        ))}
      </ul>
    </div>
  );
}
