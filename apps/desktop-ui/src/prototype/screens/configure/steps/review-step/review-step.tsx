import { StepHeader } from '@/prototype/components/step-header';

import { CommandsSection } from './components/commands-section';
import { FilesSection } from './components/files-section';
import { ValidationsSection } from './components/validations-section';
import type { ReviewStepProps } from './types';

export function ReviewStep({
  plan,
  approved,
  commands,
  resolutions,
  onToggle,
  onToggleCommand,
  onResolve
}: ReviewStepProps) {
  return (
    <>
      <StepHeader
        title="Revisão das alterações"
        lead="Confira o que será criado, atualizado ou removido e aprove o que quiser aplicar. O que você não aprovar não é tocado."
      />
      {plan.commands.length > 0 ? (
        <CommandsSection commands={plan.commands} approved={commands} onToggle={onToggleCommand} />
      ) : null}
      <FilesSection
        changes={plan.changes}
        approved={approved}
        resolutions={resolutions}
        onToggle={onToggle}
        onResolve={onResolve}
      />
      <ValidationsSection validations={plan.validations} />
    </>
  );
}
