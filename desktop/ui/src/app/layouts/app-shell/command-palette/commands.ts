import type { RecentProject } from '@/features/project-selection';
import { displayState, type ActivityFeed } from '@/features/runs';
import { executionPath, paths } from '@/shared/routing';
import { formatEngineDate, runKindLabel } from '@/shared/utils';

import { AREAS, type NavArea } from '../navigation';
import { shortcutLabel, type ShortcutId } from '../shortcuts/shortcuts';

/** O que o próprio shell sabe fazer, sem trocar de tela. */
export type ShellAction = Extract<ShortcutId, 'rail' | 'activity' | 'terminal' | 'shortcuts'>;

export type CommandAction =
  | { kind: 'go'; to: string }
  | { kind: 'open-project'; recentId: string }
  | { kind: 'pick-project' }
  | { kind: 'shell'; action: ShellAction };

export type CommandGroup = 'Ações' | 'Atividade' | 'Telas' | 'Projetos';

export interface Command {
  id: string;
  group: CommandGroup;
  label: string;
  hint: string;
  /** O que acontece ao escolher, para quem ainda não conhece o nome. */
  description?: string;
  /** Texto que só a busca enxerga, como o nome técnico de uma tela ou o caminho de um projeto. */
  keywords: string;
  action: CommandAction;
}

const ACTIONS: readonly Command[] = [
  {
    id: 'action:pick-project',
    group: 'Ações',
    label: 'Abrir pasta de projeto',
    hint: 'Projeto',
    description: 'Escolha a pasta de um projeto para trabalhar nele.',
    keywords: 'conectar novo projeto selecionar pasta trocar',
    action: { kind: 'pick-project' }
  },
  {
    id: 'action:activity',
    group: 'Ações',
    label: 'Mostrar ou ocultar a atividade',
    hint: shortcutLabel('activity'),
    description: 'O que espera você e as últimas execuções, ao lado da tela.',
    keywords: 'tarefas feed painel aprovação',
    action: { kind: 'shell', action: 'activity' }
  },
  {
    id: 'action:terminal',
    group: 'Ações',
    label: 'Abrir o terminal do projeto',
    hint: shortcutLabel('terminal'),
    keywords: 'shell console linha de comando',
    action: { kind: 'shell', action: 'terminal' }
  },
  {
    id: 'action:rail',
    group: 'Ações',
    label: 'Recolher ou expandir o menu da área',
    hint: shortcutLabel('rail'),
    keywords: 'painel lateral barra',
    action: { kind: 'shell', action: 'rail' }
  },
  {
    id: 'action:shortcuts',
    group: 'Ações',
    label: 'Ver os atalhos de teclado',
    hint: shortcutLabel('shortcuts'),
    keywords: 'ajuda teclas',
    action: { kind: 'shell', action: 'shortcuts' }
  }
];

// Telas que abrem a partir de outra e não estão no menu, mas que a busca precisa achar.
const OFF_MENU: readonly Command[] = [
  {
    id: 'screen:idea',
    group: 'Telas',
    label: 'Da ideia ao projeto',
    hint: 'Engenharia',
    description: 'Descreva uma ideia e receba especificação, arquitetura e backlog.',
    keywords: 'especificação arquitetura backlog novo projeto',
    action: { kind: 'go', to: paths.idea }
  },
  {
    id: 'screen:architecture',
    group: 'Telas',
    label: 'Arquitetura',
    hint: 'Engenharia, Visão geral',
    description: 'O que o código é hoje e a avaliação contra as referências.',
    keywords: 'camadas dependências modernização problemas',
    action: { kind: 'go', to: paths.projectArchitecture }
  }
];

function areaCommands(area: NavArea): Command[] {
  if (area.items.length === 0)
    return [
      {
        id: `screen:${area.id}`,
        group: 'Telas',
        label: area.label,
        hint: 'Aplicativo',
        description: area.description,
        keywords: '',
        action: { kind: 'go', to: area.to }
      }
    ];

  return area.items.flatMap((item): Command[] => {
    if (!item.to) return [];
    const own: Command = {
      id: `screen:${item.id}`,
      group: 'Telas',
      label: item.label,
      hint: area.label,
      description: item.description,
      keywords: item.technical ?? '',
      action: { kind: 'go', to: item.to }
    };
    // Cada aba vira um comando com o nome dela; a que é o próprio destino do item já está no comando dele.
    const tabs = (item.tabs ?? [])
      .filter((tab) => tab.to !== item.to)
      .map((tab): Command => ({
        id: `screen:${tab.id}`,
        group: 'Telas',
        label: tab.label,
        hint: `${area.label}, ${item.label}`,
        keywords: [tab.keywords, item.technical].filter(Boolean).join(' '),
        action: { kind: 'go', to: tab.to }
      }));
    return [own, ...tabs];
  });
}

function projectCommands(recents: readonly RecentProject[]): Command[] {
  return recents
    .filter((project) => project.available)
    .map((project) => ({
      id: `project:${project.id}`,
      group: 'Projetos' as const,
      label: project.displayName,
      hint: 'Abrir projeto',
      keywords: project.displayPath,
      action: { kind: 'open-project' as const, recentId: project.id }
    }));
}

// Sem texto, a lista já tem mais de trinta linhas: só as execuções mais urgentes e as mais recentes entram.
const TASK_LIMIT = 5;
const RECENT_LIMIT = 5;

function runCommands(feed: ActivityFeed | undefined): Command[] {
  if (!feed) return [];
  const toCommand = (run: ActivityFeed['tasks'][number], pending: boolean): Command => ({
    id: `run:${run.runId}`,
    group: 'Atividade',
    label: `${runKindLabel(run.kind)} · ${run.projectName}`,
    hint: displayState(run),
    description: `Iniciada ${formatEngineDate(run.startedAt)}. Abre o detalhe da execução.`,
    keywords: pending ? 'tarefa pendente' : 'execução recente',
    action: { kind: 'go', to: executionPath(run.runId) }
  });
  return [
    ...feed.tasks.slice(0, TASK_LIMIT).map((run) => toCommand(run, true)),
    ...feed.recent.slice(0, RECENT_LIMIT).map((run) => toCommand(run, false))
  ];
}

export function buildCommands(recents: readonly RecentProject[], feed?: ActivityFeed): Command[] {
  return [...ACTIONS, ...runCommands(feed), ...AREAS.flatMap(areaCommands), ...OFF_MENU, ...projectCommands(recents)];
}
