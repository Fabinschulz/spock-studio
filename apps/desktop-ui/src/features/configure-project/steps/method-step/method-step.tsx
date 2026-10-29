import { ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { PlanError } from '../../components/plan-error';
import { StepHeader } from '../../components/step-header';
import { isSupportedEnvironment } from '../../domain/ai-environments';
import type { Loadable } from '../../domain/journey-state';
import { AiEnvironmentCard } from './ai-environment-card';
import { ConfigurationSummaryCard } from './configuration-summary-card';
import { TlcCard } from './tlc-card';

interface MethodStepProps {
  discovery: Discovery;
  aiEnvironment: string | null;
  allowReinstall: boolean;
  updateTlc: boolean;
  plan: Loadable<unknown>;
  onEnvironment: (id: string) => void;
  onReinstall: (value: boolean) => void;
  onUpdateTlc: (value: boolean) => void;
  onBack: () => void;
  onPlan: () => void;
}

export function MethodStep({
  discovery,
  aiEnvironment,
  allowReinstall,
  updateTlc,
  plan,
  onEnvironment,
  onReinstall,
  onUpdateTlc,
  onBack,
  onPlan
}: MethodStepProps) {
  const reportedUnsupported = discovery.aiEnvironments.some(
    (environment) => environment.id === aiEnvironment && !environment.supported
  );
  const showUnsupportedNotice =
    aiEnvironment !== null && (!isSupportedEnvironment(aiEnvironment) || reportedUnsupported);
  const generating = plan.status === 'loading';

  return (
    <section className={styles.step}>
      <StepHeader title="Método de especificação e ambiente">
        O TLC estrutura as especificações do seu projeto, e esse método é fixo nesta fase. O Harness Engineer planeja a
        configuração das demais estruturas no ambiente de IA que você escolher.
      </StepHeader>

      <div className={styles.methodWrap}>
        <div className={styles.methodGrid}>
          <TlcCard
            tlc={discovery.tlc}
            tools={discovery.tools}
            updateTlc={updateTlc}
            allowReinstall={allowReinstall}
            onUpdateTlc={onUpdateTlc}
            onReinstall={onReinstall}
          />
          <AiEnvironmentCard detected={discovery.aiEnvironments} selected={aiEnvironment} onSelect={onEnvironment} />
          <ConfigurationSummaryCard tlcVersion={discovery.tlc.version} aiEnvironment={aiEnvironment} />
        </div>
      </div>

      {showUnsupportedNotice ? (
        <InlineNotice tone="warning" title="Ambiente sem suporte nesta fase">
          Só a declaração do projeto (mariliz.yaml) pode ser criada de forma genérica. Agentes, skills, regras e hooks
          não serão gerados para este ambiente.
        </InlineNotice>
      ) : null}

      <PlanError plan={plan} />

      <PrimaryActionFooter>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button
          variant="contained"
          onClick={onPlan}
          disabled={aiEnvironment === null || generating}
          endIcon={<ArrowRightIcon />}
        >
          {generating ? 'Gerando plano…' : 'Gerar plano'}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
