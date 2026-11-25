import { ArrowRightIcon, Button } from '@mariliz/ui-kit';

import { InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { PlanError } from '../../components/plan-error';
import { StepHeader } from '../../components/step-header';
import { isSupportedEnvironment } from '../../domain/ai-environments';
import type { Loadable } from '../../types/journey';
import { AiEnvironmentSection } from './ai-environment-section';
import { SpecificationSection } from './specification-section';

interface ModelStepProps {
  discovery: Discovery;
  aiEnvironments: readonly string[];
  allowReinstall: boolean;
  updateSpecification: boolean;
  installSpecification: boolean;
  plan: Loadable<unknown>;
  onToggleEnvironment: (id: string) => void;
  onReinstall: (value: boolean) => void;
  onUpdateSpecification: (value: boolean) => void;
  onInstallSpecification: (value: boolean) => void;
  onBack: () => void;
  onPlan: () => void;
}

export function ModelStep({
  discovery,
  aiEnvironments,
  allowReinstall,
  updateSpecification,
  installSpecification,
  plan,
  onToggleEnvironment,
  onReinstall,
  onUpdateSpecification,
  onInstallSpecification,
  onBack,
  onPlan
}: ModelStepProps) {
  const reportedUnsupported = new Set(discovery.aiEnvironments.filter((e) => !e.supported).map((e) => e.id));
  const showUnsupportedNotice = aiEnvironments.some((id) => !isSupportedEnvironment(id) || reportedUnsupported.has(id));
  const generating = plan.status === 'loading';

  return (
    <section className={styles.step}>
      <StepHeader title="Ambiente de IA">
        Escolha onde a equipe trabalha e se a estrutura de especificação entra agora. Nada é gravado antes da sua
        aprovação na revisão.
      </StepHeader>

      <AiEnvironmentSection discovery={discovery} selected={aiEnvironments} onToggle={onToggleEnvironment} />

      {showUnsupportedNotice ? (
        <InlineNotice tone="warning" title="Ambiente ainda sem suporte">
          Só a declaração do projeto ({discovery.naming.declaration}) pode ser criada de forma genérica. Agentes,
          skills, regras e hooks não serão gerados para este ambiente.
        </InlineNotice>
      ) : null}

      <SpecificationSection
        specification={discovery.specification}
        tools={discovery.tools}
        updateSpecification={updateSpecification}
        installSpecification={installSpecification}
        allowReinstall={allowReinstall}
        onUpdateSpecification={onUpdateSpecification}
        onInstallSpecification={onInstallSpecification}
        onReinstall={onReinstall}
      />

      <PlanError plan={plan} />

      <PrimaryActionFooter
        hint={aiEnvironments.length === 0 ? 'Escolha ao menos um ambiente de IA para gerar o plano.' : null}
      >
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button
          variant="contained"
          onClick={onPlan}
          disabled={aiEnvironments.length === 0 || generating}
          endIcon={<ArrowRightIcon />}
        >
          {generating ? 'Gerando plano…' : 'Gerar plano'}
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
