import type { RecentProject } from '@/features/project-selection';

import {
  APP_GROUP,
  ENGINEERING_GROUP,
  MANAGEMENT_GROUP,
  PLATFORM_GROUP,
  SETTINGS_GROUP,
  type NavGroup
} from '../navigation';

export type CommandAction = { kind: 'go'; to: string } | { kind: 'open-project'; recentId: string };

export interface Command {
  id: string;
  label: string;
  hint: string;
  /** Texto que só a busca enxerga, como o caminho de um projeto. */
  keywords: string;
  action: CommandAction;
}

const SCREEN_GROUPS: readonly NavGroup[] = [
  APP_GROUP,
  ENGINEERING_GROUP,
  MANAGEMENT_GROUP,
  PLATFORM_GROUP,
  SETTINGS_GROUP
];

function screenCommands(): Command[] {
  return SCREEN_GROUPS.flatMap((group) =>
    group.items.flatMap((item) =>
      item.to
        ? [
            {
              id: `screen:${item.id}`,
              label: item.label,
              hint: group.label ?? 'Aplicativo',
              keywords: '',
              action: { kind: 'go' as const, to: item.to }
            }
          ]
        : []
    )
  );
}

function projectCommands(recents: readonly RecentProject[]): Command[] {
  return recents
    .filter((project) => project.available)
    .map((project) => ({
      id: `project:${project.id}`,
      label: project.displayName,
      hint: 'Abrir projeto',
      keywords: project.displayPath,
      action: { kind: 'open-project' as const, recentId: project.id }
    }));
}

export function buildCommands(recents: readonly RecentProject[]): Command[] {
  return [...screenCommands(), ...projectCommands(recents)];
}
