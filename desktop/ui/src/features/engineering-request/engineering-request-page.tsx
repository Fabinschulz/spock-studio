import { ProjectChoice } from '@/features/configure-project/components/project-choice';
import { useProjectSelectionContext } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EngineError } from '@/shared/api';
import { PageHeader } from '@/shared/components/ui';
import { executionPath, pathTo } from '@/shared/routing';
import { Button } from '@synthra.io/ui-kit';
import { useId, useState } from 'react';
import { useNavigate } from 'react-router';

import styles from './engineering-request-page.module.scss';
import { startWorkflow } from './service/workflow-api';

interface EngineeringRequestPageProps {
  client: EngineClient;
}

export function EngineeringRequestPage({ client }: EngineeringRequestPageProps) {
  const navigate = useNavigate();
  const { selected } = useProjectSelectionContext();
  const goalId = useId();
  const impactId = useId();
  const approvalId = useId();

  const [goal, setGoal] = useState('');
  const [impact, setImpact] = useState(true);
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!selected) {
    return (
      <section className={styles.page}>
        <PageHeader
          title="Pedido de engenharia"
          description="Descreva o que precisa analisar. O motor executa fora do repositório até você aprovar na tela de execução."
          breadcrumb={[
            { label: 'Início', to: pathTo('home') },
            { label: 'Pedido de engenharia' }
          ]}
        />
        <ProjectChoice />
      </section>
    );
  }

  const trimmedGoal = goal.trim();
  const canSubmit = trimmedGoal.length > 0 && !busy;

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    setError(null);
    try {
      const { runId } = await startWorkflow(client, {
        projectId: selected.projectId,
        input: {
          goal: trimmedGoal,
          ...(impact ? { impact: true } : {}),
          ...(requiresApproval ? { requiresApproval: true } : {})
        }
      });
      navigate(executionPath(runId));
    } catch (cause) {
      setError(cause instanceof EngineError ? cause.message : 'Não foi possível iniciar o pedido.');
      setBusy(false);
    }
  };

  return (
    <section className={styles.page}>
      <PageHeader
        title="Pedido de engenharia"
        description="Stacks e evidências vêm da descoberta do projeto; nada é gravado no repositório nesta fase."
        breadcrumb={[
          { label: 'Início', to: pathTo('home') },
          { label: 'Pedido de engenharia' }
        ]}
      />

      <p className={styles.project}>
        Projeto: <strong>{selected.displayName}</strong> ({selected.displayPath})
      </p>

      <div className={styles.field}>
        <label className={styles.label} htmlFor={goalId}>
          Objetivo
        </label>
        <textarea
          id={goalId}
          className={styles.textarea}
          value={goal}
          onChange={(event) => setGoal(event.target.value)}
          placeholder="Ex.: descrever impacto de alterar o contrato de autenticação"
          required
        />
      </div>

      <div className={styles.checkboxRow}>
        <input
          id={impactId}
          type="checkbox"
          checked={impact}
          onChange={(event) => setImpact(event.target.checked)}
        />
        <label htmlFor={impactId}>Incluir análise de impacto técnico</label>
      </div>

      <div className={styles.checkboxRow}>
        <input
          id={approvalId}
          type="checkbox"
          checked={requiresApproval}
          onChange={(event) => setRequiresApproval(event.target.checked)}
        />
        <label htmlFor={approvalId}>Pausar para aprovação antes de concluir</label>
      </div>

      {error ? (
        <p role="alert">
          {error}
        </p>
      ) : null}

      <div className={styles.actions}>
        <Button type="button" onClick={() => void submit()} disabled={!canSubmit}>
          {busy ? 'Iniciando…' : 'Iniciar pedido'}
        </Button>
        <Button type="button" variant="outlined" color="inherit" onClick={() => navigate(pathTo('configure'))}>
          Configurar projeto
        </Button>
      </div>
    </section>
  );
}
