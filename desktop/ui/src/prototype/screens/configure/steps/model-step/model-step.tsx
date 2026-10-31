import { StepHeader } from '@/prototype/components/step-header';
import { WorkingNotice } from '@/prototype/components/working-notice';

import { EnvironmentSection } from './components/environment-section';
import { SpecificationSection } from './components/specification-section';
import type { ModelStepProps } from './types';

export function ModelStep({ env, onEnv, planning }: ModelStepProps) {
  return (
    <>
      <StepHeader
        title="Modelo e ambiente"
        lead="O modelo de implementação Mariliz organiza a engenharia do projeto em agentes, skills, regras, hooks e validações. Escolha o ambiente de IA em que você trabalha."
      />

      <SpecificationSection />
      <EnvironmentSection env={env} onEnv={onEnv} disabled={planning} />

      {planning ? (
        <WorkingNotice
          title="Montando o plano…"
          note="O motor monta o plano numa única chamada e não informa o andamento. Nada é alterado no projeto."
        />
      ) : null}
    </>
  );
}
