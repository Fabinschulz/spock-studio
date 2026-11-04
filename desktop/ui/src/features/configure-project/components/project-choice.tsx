import { ProjectStep } from '../steps/project-step';

/** A primeira etapa quando ainda não há pasta escolhida: só escolher uma pasta ou reabrir uma recente. */
export function ProjectChoice() {
  return (
    <ProjectStep
      project={null}
      discovery={{ status: 'idle' }}
      aiEnvironment={null}
      onEnvironment={() => undefined}
      onAnalyze={() => undefined}
      onContinue={() => undefined}
    />
  );
}
