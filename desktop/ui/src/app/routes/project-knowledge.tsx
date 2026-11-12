import { KnowledgePage } from '@/features/project';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function ProjectKnowledge() {
  const { selected } = useProjectSelectionContext();
  return <KnowledgePage client={tauriEngineClient} project={selected} />;
}
