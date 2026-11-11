import type { AssistantAnswer, AssistantStatement } from '@/shared/schema';
import { useId } from 'react';

import { ANSWER_SECTIONS } from '../domain/assistant';
import styles from '../management-page.module.scss';

/**
 * A resposta conferida, separada como o pedido exige. Cada afirmação mostra os fatos que cita; o que a verificação
 * descartou continua visível, com o motivo, para que nada suma sem explicação.
 */
export function AssistantAnswerView({ answer }: { answer: AssistantAnswer }) {
  const facts = new Map(answer.evidence.map((fact) => [fact.id, fact.text]));

  return (
    <section aria-labelledby="resposta" className={styles.answer}>
      <h2 id="resposta" className={styles.section}>
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
        <details className={styles.method}>
          <summary>
            {answer.rejected.length === 1
              ? '1 afirmação descartada pela verificação'
              : `${answer.rejected.length} afirmações descartadas pela verificação`}
          </summary>
          <ul>
            {answer.rejected.map((item) => (
              <li key={`${item.section}:${item.text}`}>
                <span className={styles.kindLabel}>{item.section}:</span> {item.text} <em>({item.reason})</em>
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      <p className={styles.ruleNote}>
        Escrito por {answer.model} a partir dos fatos do projeto e conferido pelo Spock Studio: cada afirmação cita os
        fatos que usou, e número que não está neles é descartado.
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
            <details className={styles.sources}>
              <summary>
                {statement.evidence.length === 1 ? '1 fato citado' : `${statement.evidence.length} fatos citados`}
              </summary>
              <ul>
                {statement.evidence.map((id) => (
                  <li key={id}>{facts.get(id) ?? id}</li>
                ))}
              </ul>
            </details>
          </li>
        ))}
      </ul>
    </div>
  );
}
