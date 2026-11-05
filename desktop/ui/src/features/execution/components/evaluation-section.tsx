import type { WorkflowSnapshot } from '../domain/run-timeline';
import styles from './workspace.module.scss';

/** Nota mínima por critério (critérios versão 1); abaixo dela a opinião é "reprovada". Igual a PASSING_SCORE no motor. */
const PASSING_SCORE = 3;
const CRITERION_LABEL: Record<string, string> = {
  plan_quality: 'Qualidade do plano',
  context_fit: 'Uso do conteúdo do projeto',
  request_fit: 'Atendimento ao pedido',
  architecture_coherence: 'Coerência arquitetural'
};

type Evaluation = NonNullable<NonNullable<WorkflowSnapshot['result']>['evaluation']>;

/** A opinião do juiz por modelo, como o motor a registrou. É um registro: não decide o resultado da execução. */
export function EvaluationSection({ evaluation }: { evaluation: Evaluation }) {
  const judgment = evaluation.judgment;
  if (!judgment) return null;
  return (
    <section aria-labelledby="avaliacao-titulo">
      <h2 id="avaliacao-titulo">Avaliação por modelo</h2>
      <p>
        Uma opinião registrada por um avaliador independente. Ela não decide o resultado da execução.{' '}
        <strong>
          {evaluation.judge === 'passed'
            ? 'Todos os critérios alcançaram a nota mínima.'
            : 'Algum critério ficou abaixo da nota mínima.'}
        </strong>
      </p>
      <ul className={styles.list}>
        {Object.entries(judgment.scores).map(([criterion, score]) => (
          <li key={criterion}>
            {CRITERION_LABEL[criterion] ?? criterion}: {score} de 5{score < PASSING_SCORE ? ' (abaixo do mínimo)' : ''}
          </li>
        ))}
      </ul>
      <p>{judgment.rationale}</p>
      <p>
        Modelo: {judgment.model} ({judgment.modelVersion}), critérios versão {judgment.criteriaVersion}.
      </p>
    </section>
  );
}
