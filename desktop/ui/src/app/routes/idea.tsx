import { IdeaPage } from '@/features/idea';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function Idea() {
  const { selected } = useProjectSelectionContext();
  return <IdeaPage client={tauriEngineClient} project={selected} />;
}
