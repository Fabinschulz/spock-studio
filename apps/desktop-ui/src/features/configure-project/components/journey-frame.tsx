import type { ReactNode } from 'react';

import { PageHeader, WizardSteps, type StepIssue } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';

import { journeySteps, type JourneyStepId } from '../domain/journey-state';
import styles from '../configure-project-page.module.scss';
import { ImplementationModelBadge } from './implementation-model-badge';

interface JourneyFrameProps {
  step: JourneyStepId;
  issue?: StepIssue;
  children: ReactNode;
}

export function JourneyFrame({ step, issue, children }: JourneyFrameProps) {
  return (
    <div className={styles.page}>
      <PageHeader
        breadcrumb={[{ label: 'Projetos', to: pathTo('configure') }, { label: 'Configurar projeto' }]}
        lead={<WizardSteps steps={journeySteps} currentId={step} issue={issue} />}
        title="Configurar projeto"
        actions={<ImplementationModelBadge />}
        description="Vamos analisar o seu projeto e preparar o ambiente de engenharia."
      />
      {children}
    </div>
  );
}
