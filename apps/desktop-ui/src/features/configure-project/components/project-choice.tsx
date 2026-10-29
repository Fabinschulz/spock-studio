import { useProjectSelectionContext } from '@/features/project-selection';

import { ProjectStep } from '../steps/project-step';

/** A primeira etapa quando ainda não há pasta escolhida: só escolher uma pasta ou reabrir uma recente. */
export function ProjectChoice() {
  const { busy, error, cancelled, recents, pick, openRecent, unavailableReason } = useProjectSelectionContext();

  return (
    <ProjectStep
      project={null}
      discovery={{ status: 'idle' }}
      recents={recents}
      busy={busy}
      error={error}
      cancelled={cancelled}
      unavailableReason={unavailableReason}
      onPick={() => void pick()}
      onOpenRecent={(id) => void openRecent(id)}
      aiEnvironment={null}
      onEnvironment={() => undefined}
      onAnalyze={() => undefined}
      onContinue={() => undefined}
    />
  );
}
