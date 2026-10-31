import { StepHeader } from '@/prototype/components/step-header';
import { NO_PROPOSAL_CODE } from '@/prototype/constants/harness';

import { CreatedFilesSection } from './components/created-files-section';
import { NextStepNotice } from './components/next-step-notice';
import { OutcomeNotice } from './components/outcome-notice';
import { ValidationSection } from './components/validation-section';
import type { ResultStepProps } from './types';

export function ResultStep({ result }: ResultStepProps) {
  const evals = result.validation?.results ?? [];
  const created = result.files?.created ?? [];
  const noProposal = result.limitations?.some((l) => l.code === NO_PROPOSAL_CODE);

  return (
    <>
      <StepHeader
        title="Configuração aplicada"
        lead="O Harness do projeto foi escrito e validado. Você pode desfazer tudo pelo backup guardado antes da aplicação."
      />
      <OutcomeNotice result={result} evals={evals} />
      <ValidationSection evals={evals} />
      <CreatedFilesSection files={created} />
      {noProposal ? <NextStepNotice /> : null}
    </>
  );
}
