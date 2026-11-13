import { ProjectStep } from '../steps/project-step';

export function ProjectChoice() {
  return (
    <ProjectStep
      project={null}
      discovery={{ status: 'idle' }}
      onAnalyze={() => undefined}
      onContinue={() => undefined}
    />
  );
}
