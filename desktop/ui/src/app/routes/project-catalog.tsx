import { CatalogPage } from '@/features/project';
import { useProjectSelectionContext } from '@/features/project-selection';
import { tauriEngineClient } from '@/shared/api';

export default function ProjectCatalog() {
  const { selected } = useProjectSelectionContext();
  return <CatalogPage client={tauriEngineClient} project={selected} />;
}
