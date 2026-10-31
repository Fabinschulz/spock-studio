import { ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { PlanError } from '../../components/plan-error';
import { StepHeader } from '../../components/step-header';
import { isSupportedEnvironment } from '../../domain/ai-environments';
import type { Loadable } from '../../types/journey';
import { AiEnvironmentCard } from './ai-environment-card';
import { ConfigurationSummaryCard } from './configuration-summary-card';
import { SpecificationCard } from './specification-card';

interface ModelStepProps {
  discovery: Discovery;
  aiEnvironment: string | null;
  allowReinstall: boolean;
  updateSpecification: boolean;
  plan: Loadable<unknown>;
  onEnvironment: (id: string) => void;
  onReinstall: (value: boolean) => void;
  onUpdateSpecification: (value: boolean) => void;
  onBack: () => void;
  onPlan: () => void;
}

export function ModelStep({
  discovery,
  aiEnvironment,
  allowReinstall,
  updateSpecification,
  plan,
  onEnvironment,
  onReinstall,
  onUpdateSpecification,
  onBack,
  onPlan
}: ModelStepProps) {
  const reportedUnsupported = discovery.aiEnvironments.some(
    (environment) => environment.id === aiEnvironment && !environment.supported
  );
  const showUnsupportedNotice =
    aiEnvironment !== null && (!isSupportedEnvironment(aiEnvironment) || reportedUnsupported);
  const generating = plan.status === 'loading';

  return (
    <section className={styles.step}>
      <StepHeader title="Modelo de implementação e ambiente">
        Vamos preparar a estrutura de engenharia do seu projeto, configurando especificações, agentes, ferramentas,
        regras e validações de acordo com as necessidades identificadas. O Harness Engineer planeja a configuração no
        ambiente de IA que você escolher.
      </StepHeader>

      <div className={styles.modelWrap}>
        <div className={styles.modelGrid}>
          <SpecificationCard
            specification={discovery.specification}
            tools={discovery.tools}
            updateSpecification={updateSpecification}
            allowReinstall={allowReinstall}
            onUpdateSpecification={onUpdateSpecification}
            onReinstall={onReinstall}
          />
          <AiEnvironmentCard detected={discovery.aiEnvironments} selected={aiEnvironment} onSelect={onEnvironment} />
          <ConfigurationSummaryCard
            specificationVersion={discovery.specification.version}
            aiEnvironment={aiEnvironment}
          />
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
