import { Button } from '@synthra.io/ui-kit';

import { ProjectComposer } from '@/prototype/components/project-composer';
import { StepHeader } from '@/prototype/components/step-header';
import { WorkingNotice } from '@/prototype/components/working-notice';
import screen from '@/prototype/styles/screen.module.scss';

import { CurrentState } from './components/current-state';
import { DetectedCommands } from './components/detected-commands';
import { StackFindings } from './components/stack-findings';
import type { ProjectStepProps } from './types';

const ANALYZE_LABEL = { idle: 'Analisar projeto', loading: 'Analisando…', ready: 'Analisar de novo' } as const;

export function ProjectStep({ analysis, onAnalyze }: ProjectStepProps) {
  const idle = analysis === 'idle';

  return (
    <>
      <StepHeader
        title="Analisar o projeto"
        lead="O Mariliz lê a estrutura do projeto para entender stack, comandos e o que já existe. Nada é alterado."
      />

      <ProjectComposer>
        <Button
          variant={idle ? 'contained' : 'outlined'}
          color={idle ? 'primary' : 'inherit'}
          disabled={analysis === 'loading'}
          onClick={onAnalyze}
        >
          {ANALYZE_LABEL[analysis]}
        </Button>
      </ProjectComposer>

      {idle ? <p className={screen.note}>A análise é somente de leitura.</p> : null}

      {analysis === 'loading' ? (
        <WorkingNotice
          title="Lendo o projeto…"
          note="O motor não informa o andamento desta etapa; assim que terminar, o resultado aparece aqui."
        />
      ) : null}

      {analysis === 'ready' ? (
        <>
          <StackFindings />
          <DetectedCommands />
          <CurrentState />
        </>
      ) : null}
    </>
  );
}
