import type { RecentProject } from '@/features/project-selection';
import { paths } from '@/shared/routing';

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

// A ideia saiu do menu (é jornada de começo, está no Início), mas a busca ainda a acha.
const OFF_MENU: readonly Command[] = [
  {
    id: 'screen:idea',
    label: 'Da ideia ao projeto',
    hint: 'Engenharia',
    keywords: 'especificação arquitetura backlog',
    action: { kind: 'go', to: paths.idea }
  }
];

function screenCommands(): Command[] {
  const screens = SCREEN_GROUPS.flatMap((group) =>
    group.items.flatMap((item): Command[] => {
      if (!item.to) return [];
      const hint = group.label ?? 'Aplicativo';
      const own = {
        id: `screen:${item.id}`,
        label: item.label,
        hint,
        keywords: '',
        action: { kind: 'go' as const, to: item.to }
      };
      // Cada aba vira um comando com o nome dela; a que é o próprio destino do item já está no comando dele.
      const tabs = (item.tabs ?? [])
        .filter((tab) => tab.to !== item.to)
        .map((tab) => ({
          id: `screen:${tab.id}`,
          label: tab.label,
          hint: `${hint}, ${item.label}`,
          keywords: tab.keywords ?? '',
          action: { kind: 'go' as const, to: tab.to }
        }));
      return [own, ...tabs];
    })
  );
  return [...screens, ...OFF_MENU];
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
