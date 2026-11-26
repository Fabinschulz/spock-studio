import { ErrorState, InlineNotice, LoadingState, PageSection, StatusChip, Term } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { ContextReview } from '@/shared/schema';
import { Link } from 'react-router';

import type { ContextReviewState } from '../hooks/use-context-review';
import styles from './context-review-panel.module.scss';

const DECISION = '@decision/';
const EXTERNAL = '@external/';
const ANALYSIS = '@analysis/';

const number = (value: number) => value.toLocaleString('pt-BR');

/** Decisões, fontes externas e a análise de arquitetura não são arquivos: o nome diz de onde o trecho veio. */
export function sourceLabel(path: string) {
  if (path.startsWith(DECISION)) return 'Decisão registrada';
  if (path.startsWith(ANALYSIS)) return `Análise de arquitetura: ${path.slice(ANALYSIS.length).replace(/\.md$/, '')}`;
  if (path.startsWith(EXTERNAL)) return `Fonte externa: ${path.slice(EXTERNAL.length).split('/').slice(-2).join('/')}`;
  return path;
}

export function ContextReviewPanel({ state }: { state: ContextReviewState }) {
  if (state.status === 'idle') return null;
  if (state.status === 'loading') return <LoadingState>Montando o pedido como ele iria ao modelo…</LoadingState>;
  if (state.status === 'failed') return <ErrorState>{state.message}</ErrorState>;
  return <Review review={state.review} />;
}

function Review({ review }: { review: ContextReview }) {
  const fits = review.plannedTokens <= review.budget;
  const included = review.evidence.filter((piece) => piece.included);
  const cut = review.evidence.filter((piece) => !piece.included);

  return (
    <PageSection
      title="Contexto do pedido"
      action={
        <StatusChip tone={fits ? 'success' : 'warning'}>{fits ? 'Cabe no orçamento' : 'Passa do orçamento'}</StatusChip>
      }
    >
      <p className={styles.summary}>
        No pior caso, a execução usa {number(review.plannedTokens)} de {number(review.budget)}{' '}
        <Term id="token">tokens</Term>: a entrada de cada etapa mais a resposta máxima dela. O teto dos fluxos de
        engenharia se ajusta em Conexões › Modelos de IA.
      </p>
      <meter
        className={styles.meter}
        min={0}
        max={review.budget}
        high={review.budget}
        value={Math.min(review.plannedTokens, review.budget)}
        aria-label="Uso do orçamento de tokens"
      />
      <Counting review={review} />

      <ul className={styles.calls}>
        {review.calls.map((call) => (
          <li key={call.agentId}>
            <strong>{call.label}</strong>: {number(call.inputTokens)} de entrada e até {number(call.outputTokens)} de
            resposta.
            <ul className={styles.sections}>
              {call.sections.map((section) => (
                <li key={section.id}>
                  {section.label}: {number(section.tokens)}
                </li>
              ))}
            </ul>
            {call.withinBudget ? null : (
              <p className={styles.warning}>
                Esta etapa passa do orçamento: o modelo pode não rodar nela, e o resultado segue pelas regras do motor.
              </p>
            )}
          </li>
        ))}
      </ul>
      <p className={styles.note}>O juiz roda depois, sobre o resultado; o gasto dele não entra nesta prévia.</p>

      <h3 className={styles.heading}>Trechos que entram ({included.length})</h3>
      {included.length === 0 ? (
        <p className={styles.note}>Nenhum trecho do projeto corresponde ao pedido.</p>
      ) : (
        <ul className={styles.pieces}>
          {included.map((piece) => (
            <li key={`${piece.path}-${piece.tokens}`}>
              <span className={styles.path}>{sourceLabel(piece.path)}</span> · {number(piece.tokens)} tokens
              {piece.truncated ? ' · cortado no tamanho máximo' : ''}
            </li>
          ))}
        </ul>
      )}

      <LeftOut review={review} cut={cut.map((piece) => sourceLabel(piece.path))} />
    </PageSection>
  );
}

function Counting({ review }: { review: ContextReview }) {
  if (!review.model.configured) {
    return (
      <InlineNotice title="Nenhum modelo configurado">
        Sem modelo, nada sai do computador e o fluxo segue pelas regras do motor. Os números abaixo são estimativa do
        que seria enviado.
      </InlineNotice>
    );
  }
  return (
    <p className={styles.note}>
      {review.counting.exact ? 'Contagem exata' : 'Estimativa'} para {review.model.model}. {review.counting.method}
    </p>
  );
}

function LeftOut({ review, cut }: { review: ContextReview; cut: string[] }) {
  const { leftOut } = review;
  const excluded = leftOut.excluded.reduce((total, item) => total + item.count, 0);
  if (cut.length === 0 && leftOut.pending === 0 && excluded === 0) {
    return <p className={styles.note}>Nada do que a busca achou ficou de fora.</p>;
  }
  return (
    <>
      <h3 className={styles.heading}>O que ficou de fora</h3>
      <ul className={styles.pieces}>
        {cut.length > 0 ? <li>Não couberam no bloco de trechos: {cut.join(', ')}.</li> : null}
        {leftOut.pending > 0 ? (
          <li>
            {number(leftOut.pending)}{' '}
            {leftOut.pending === 1 ? 'arquivo ainda não foi indexado' : 'arquivos ainda não foram indexados'} e entra
            {leftOut.pending === 1 ? '' : 'm'} nas próximas execuções.
          </li>
        ) : null}
        {leftOut.excluded.map((item) => (
          <li key={item.reason}>
            {item.label}: {number(item.count)}
          </li>
        ))}
      </ul>
      {excluded > 0 ? (
        <p className={styles.note}>
          O que a política barra nunca vai ao modelo. Para ver ou mudar, abra{' '}
          <Link to={pathTo('projectKnowledge')}>Conhecimento</Link>.
        </p>
      ) : null}
    </>
  );
}
