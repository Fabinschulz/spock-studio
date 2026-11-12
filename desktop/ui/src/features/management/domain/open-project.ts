import type { RecentProject, SelectedProject } from '@/features/project-selection';
import type { PortfolioProject } from '@/shared/schema';

/** O que a linha do portfólio pode fazer pelo projeto. `null`: a interface não tem como abri-lo daqui. */
export type OpenAction = { kind: 'current' } | { kind: 'recent'; recentId: string } | null;

/**
 * O host guarda os projetos recentes por caminho e não conhece o `projectId` do motor. Por isso só o projeto aberto agora
 * casa por id. Os demais casam pelo nome e pelo caminho abreviado que o motor e o host mostram, e só quando exatamente um
 * recente combina: na dúvida, nenhuma ação, em vez de abrir o projeto errado.
 */
export function resolveOpenAction(
  project: Pick<PortfolioProject, 'projectId' | 'displayName' | 'displayPath'>,
  selected: SelectedProject | null,
  recents: readonly RecentProject[]
): OpenAction {
  if (selected?.projectId === project.projectId) return { kind: 'current' };

  const matches = recents.filter(
    (recent) =>
      recent.available && recent.displayName === project.displayName && recent.displayPath === project.displayPath
  );
  const [only] = matches;
  return matches.length === 1 && only ? { kind: 'recent', recentId: only.id } : null;
}
