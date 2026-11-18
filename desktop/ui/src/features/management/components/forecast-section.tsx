import { StatusChip, PageSection, Disclosure, Term } from '@/shared/components';
import type { Connector, DeliveryForecast, ProjectForecast } from '@/shared/schema';
import { useId } from 'react';

import { CONFIDENCE_LABEL } from '../domain/delivery-labels';
import { formatDay, formatWeeks } from '../domain/format';
import styles from '../management-page.module.scss';

interface ForecastSectionProps {
  forecast: ProjectForecast;
  connectors: Connector[];
}

/** Sem base suficiente, a seção diz que não há previsão em vez de mostrar uma data. */
export function ForecastSection({ forecast, connectors }: ForecastSectionProps) {
  if (forecast.kinds.length === 0) return null;
  const label = (kind: string) =>
    connectors.flatMap((connector) => connector.dataKinds).find((entry) => entry.id === kind)?.label ?? kind;

  return (
    <PageSection title="Previsão de entrega">
      {forecast.kinds.map((kind) => (
        <KindForecast key={kind.kind} forecast={kind} label={label(kind.kind)} />
      ))}
      <Disclosure className={styles.method} summary="Como a previsão é calculada">
        <p>{forecast.method}</p>
        <p className={styles.methodHeading}>Premissas</p>
        <ul>
          {forecast.assumptions.map((text) => (
            <li key={text}>{text}</li>
          ))}
        </ul>
        <p className={styles.methodHeading}>O que não é considerado</p>
        <ul>
          {forecast.limitations.map((text) => (
            <li key={text}>{text}</li>
          ))}
        </ul>
      </Disclosure>
    </PageSection>
  );
}

function KindForecast({ forecast, label }: { forecast: DeliveryForecast; label: string }) {
  const titleId = useId();

  return (
    <div className={styles.kind} role="group" aria-labelledby={titleId}>
      <div className={styles.portfolioHead}>
        <h3 id={titleId} className={styles.kindTitle}>
          {label}: {forecast.remaining} {forecast.remaining === 1 ? 'aberto' : 'abertos'}
        </h3>
        {forecast.confidence ? (
          <StatusChip tone={CONFIDENCE_LABEL[forecast.confidence].tone}>
            {CONFIDENCE_LABEL[forecast.confidence].label}
          </StatusChip>
        ) : null}
      </div>

      {forecast.unavailable ? (
        <p className={styles.muted}>Sem previsão. {forecast.unavailable}</p>
      ) : (
        <>
          <dl className={styles.figures}>
            {forecast.scenarios.map((scenario) => (
              <div key={scenario.id} className={styles.figure}>
                <dt>
                  {scenario.label}
                  <span className={styles.figureNote}>
                    {formatWeeks(scenario.weeks)}; {scenario.percentile}% de chance de terminar até esta data, se o
                    ritmo das últimas semanas se mantiver
                  </span>
                </dt>
                <dd>{formatDay(scenario.by)}</dd>
              </div>
            ))}
          </dl>
          <p className={styles.muted}>
            Base: {forecast.historyWeeks} semanas de histórico, com {forecast.weeklyThroughput.join(', ')} entregas por
            semana; {forecast.trials.toLocaleString('pt-BR')} <Term id="simulation" />.
          </p>
          {forecast.confidenceReasons.length > 0 ? (
            <ul className={styles.notes} aria-label={`Por que a confiança não é alta (${label})`}>
              {forecast.confidenceReasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
          ) : null}
          {forecast.riskFactors.length > 0 ? (
            <ul className={styles.signals} aria-label={`Fatores que podem atrasar (${label})`}>
              {forecast.riskFactors.map((factor) => (
                <li key={factor}>{factor}</li>
              ))}
            </ul>
          ) : null}
        </>
      )}
    </div>
  );
}
