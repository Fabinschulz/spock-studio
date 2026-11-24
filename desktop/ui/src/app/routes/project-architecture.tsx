import { ArchitecturePage } from '@/features/project';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function ProjectArchitecture() {
  const { selected } = useProjectSelectionContext();
  return <ArchitecturePage client={tauriEngineClient} project={selected} />;
}
