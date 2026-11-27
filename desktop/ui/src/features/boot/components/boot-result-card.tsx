import { Disclosure, InlineNotice } from '@/shared/components';
import type { BootRecord, BootStep } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';

import { OUTCOME_LABEL } from '../domain/boot-labels';
import styles from './boot-result-card.module.scss';

const STEP_LABEL: Record<BootStep['status'], string> = {
  pending: 'aguardando',
  running: 'em andamento',
  completed: 'feito',
  skipped: 'não se aplica',
  failed: 'falhou',
  blocked: 'bloqueado'
};

interface BootResultCardProps {
  record: BootRecord;
  onOpen: (route: string) => void;
  onChoose: (capabilityId: string) => void;
  onDismiss: () => void;
}

function List({ title, items, empty }: { title: string; items: readonly string[]; empty?: string }) {
  if (items.length === 0 && !empty) return null;
  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>{title}</h3>
      {items.length === 0 ? (
        <p className={styles.muted}>{empty}</p>
      ) : (
        <ul className={styles.list}>
          {items.map((item, index) => (
            // O motor não garante linhas diferentes entre si: o texto sozinho repetiria a chave.
            <li key={`${index}-${item}`}>{item}</li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * O que o Spock entendeu, conferiu e fez com o pedido. Texto vindo do motor: a interface não escreve conclusão, só mostra o
 * que cada etapa deixou registrado, com as etapas à vista para quem quiser conferir.
 */
export function BootResultCard({ record, onOpen, onChoose, onDismiss }: BootResultCardProps) {
  const result = record.result;
  if (!result) {
    return (
      <InlineNotice tone="warning">
        O preparo foi interrompido antes de terminar. Peça de novo; o que já foi lido não se perde.
      </InlineNotice>
    );
  }

  const showChanges = result.outcome === 'answered' || result.outcome === 'prepared' || result.outcome === 'handoff';
  return (
    <section className={styles.card} aria-label="Resultado do preparo do pedido" aria-live="polite">
      <header className={styles.header}>
        <span className={styles.badge} data-outcome={result.outcome}>
          {OUTCOME_LABEL[result.outcome]}
        </span>
        <h2 className={styles.headline}>{result.headline}</h2>
        {record.plan ? <p className={styles.muted}>Objetivo: {record.plan.objective}</p> : null}
      </header>

      {result.alternatives.length > 0 ? (
        <section className={styles.section}>
          <h3 className={styles.sectionTitle}>
            {result.outcome === 'needsClarification' ? 'Qual delas?' : 'Você pode ir por aqui'}
          </h3>
          <ul className={styles.options}>
            {result.alternatives.map((alternative) => (
              <li key={alternative.capabilityId}>
                <Button variant="outlined" size="small" onClick={() => onChoose(alternative.capabilityId)}>
                  {alternative.name}
                </Button>
                <span className={styles.muted}> {alternative.reason}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <List title="O que foi feito" items={result.done} />
      <List title="O que foi consultado" items={result.consulted} />
      {showChanges ? (
        <List title="Alterações" items={result.changes} empty="Nenhuma alteração foi feita no projeto." />
      ) : null}
      <List title="O que foi conferido" items={result.validations} />
      {result.problems.length > 0 ? (
        <section className={styles.section}>
          <h3 className={styles.sectionTitle}>Problemas e limitações</h3>
          <InlineNotice tone={result.outcome === 'failed' ? 'danger' : 'warning'}>
            <ul className={styles.list}>
              {result.problems.map((problem, index) => (
                <li key={`${index}-${problem}`}>{problem}</li>
              ))}
            </ul>
          </InlineNotice>
        </section>
      ) : null}
      <List title="Pendente" items={result.pending} />

      <footer className={styles.footer}>
        {result.nextStep ? <p className={styles.next}>Próximo passo: {result.nextStep}</p> : null}
        <div className={styles.actions}>
          {result.route ? (
            <Button variant="contained" onClick={() => onOpen(result.route!)}>
              {result.routeLabel ?? 'Abrir'}
            </Button>
          ) : null}
          <Button variant="text" color="inherit" onClick={onDismiss}>
            Fechar
          </Button>
        </div>
      </footer>

      {record.steps.length > 0 ? (
        <Disclosure summary={`Ver as etapas (${record.steps.length})`}>
          <ol className={styles.steps}>
            {record.steps.map((step) => (
              <li key={step.id}>
                <strong>{step.title}</strong> <span className={styles.muted}>({STEP_LABEL[step.status]})</span>
                {step.summary ? <div>{step.summary}</div> : null}
              </li>
            ))}
          </ol>
        </Disclosure>
      ) : null}
    </section>
  );
}
