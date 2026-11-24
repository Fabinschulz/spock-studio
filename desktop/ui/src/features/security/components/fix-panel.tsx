import { ConfirmGate, DiffView, ErrorState, InlineNotice, LoadingState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { SecurityFixResult, SecurityPlan } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import type { SecurityFix } from '../hooks/use-security-fix';
import styles from '../security-page.module.scss';

const count = (value: number, one: string, many: string) => `${value} ${value === 1 ? one : many}`;

interface FixPanelProps {
  fix: SecurityFix;
  onApply: (plan: SecurityPlan) => void;
  onRevert: (result: SecurityFixResult) => void;
  onClose: () => void;
}

export function FixPanel({ fix, onApply, onRevert, onClose }: FixPanelProps) {
  switch (fix.status) {
    case 'idle':
      return null;
    case 'planning':
      return <LoadingState>Montando a correção…</LoadingState>;
    case 'applying':
      return <LoadingState>Aplicando a correção e varrendo o projeto de novo…</LoadingState>;
    case 'reverting':
      return <LoadingState>Desfazendo a correção…</LoadingState>;
    case 'failed':
      return <ErrorState>{fix.message}</ErrorState>;
    case 'reverted':
      return (
        <InlineNotice tone="info" title="Correção desfeita">
          Os arquivos voltaram ao que eram antes. A varredura abaixo já mostra o estado atual.
        </InlineNotice>
      );
    case 'applied':
      return <Applied result={fix.result} onRevert={onRevert} />;
    case 'reviewing':
      return <Review plan={fix.plan} onApply={onApply} onClose={onClose} />;
  }
}

function Review({
  plan,
  onApply,
  onClose
}: {
  plan: SecurityPlan;
  onApply: (plan: SecurityPlan) => void;
  onClose: () => void;
}) {
  return (
    <PageSection title="Revisar a correção">
      <p className={styles.note}>
        Corrige {count(plan.findingIds.length, 'achado', 'achados')} mudando{' '}
        {count(plan.changes.length, 'arquivo', 'arquivos')}, exatamente como abaixo.{' '}
        {plan.manual > 0 ? `${count(plan.manual, 'achado continua', 'achados continuam')} com você.` : ''}
      </p>
      {plan.changes.map((change) => (
        <div key={change.path} className={styles.change}>
          <h3 className={styles.changeTitle}>
            {change.path} · {change.op === 'create' ? 'arquivo novo' : 'alterado'}
          </h3>
          <DiffView diff={change.diff} label={change.path} />
        </div>
      ))}
      <ConfirmGate
        label="Aplicar a correção de segurança"
        confirmLabel="Aplicar"
        message="Os arquivos acima mudam como mostrado. Uma cópia fica guardada e dá para desfazer depois."
        onConfirm={() => onApply(plan)}
      >
        {(ask) => (
          <div className={styles.actions}>
            <Button type="button" onClick={ask}>
              Aplicar correção
            </Button>
            <Button type="button" variant="outlined" color="inherit" onClick={onClose}>
              Agora não
            </Button>
          </div>
        )}
      </ConfirmGate>
    </PageSection>
  );
}

function Applied({ result, onRevert }: { result: SecurityFixResult; onRevert: (result: SecurityFixResult) => void }) {
  return (
    <InlineNotice tone="success" title="Correção aplicada">
      {count(result.fixed, 'achado resolvido', 'achados resolvidos')}
      {result.remaining === null ? '.' : `; ${count(result.remaining, 'continua', 'continuam')} na lista abaixo.`} O
      registro fica em <Link to={pathTo('runs')}>Execuções</Link>.
      <div className={styles.actions}>
        <ConfirmGate
          label="Desfazer a correção de segurança"
          confirmLabel="Desfazer"
          message="Os arquivos voltam ao que eram antes da correção."
          onConfirm={() => onRevert(result)}
        >
          {(ask) => (
            <Button type="button" variant="outlined" color="inherit" onClick={ask}>
              Desfazer correção
            </Button>
          )}
        </ConfirmGate>
      </div>
    </InlineNotice>
  );
}
