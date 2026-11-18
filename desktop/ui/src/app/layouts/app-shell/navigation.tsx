import { pathTo } from '@/shared/routing';
import {
  AssignmentIcon,
  BookOpenIcon,
  BriefcaseIcon,
  CalendarIcon,
  ChartBarIcon,
  ChatIcon,
  GlobeIcon,
  GridIcon,
  Home01Icon,
  IdeaIcon,
  InfoIcon,
  IntersectingArrowsIcon,
  ListViewRectangleIcon,
  PlayIcon,
  ReportIcon,
  ShieldAlertIcon,
  ShieldCheckIcon
} from '@mariliz/ui-kit';
import type { ReactNode } from 'react';
import type { Environment } from './environment';
/** Uma tela que divide o item do menu com outras. `keywords` guarda o nome antigo, para a busca ainda achá-la por ele. */
export interface NavTab {
  id: string;
  label: string;
  to: string;
  keywords?: string;
}

export interface NavItem {
  id: string;
  label: string;
  icon: ReactNode;
  /** Sem destino = indisponível; `unavailableReason` diz por quê. */
  to?: string;
  end?: boolean;
  unavailableReason?: string;
  activity?: { label: string; text: string; tone: 'warning' | 'info' };
  /** O item fica marcado em qualquer uma delas, e a página mostra as abas para ir de uma à outra. */
  tabs?: readonly NavTab[];
}

/** Sem rótulo, o grupo é do aplicativo como um todo. */
export interface NavGroup {
  id: string;
  label?: string;
  items: NavItem[];
}

export const APP_GROUP: NavGroup = {
  id: 'app',
  items: [{ id: 'home', label: 'Início', icon: <Home01Icon fontSize="small" />, to: pathTo('home'), end: true }]
};

export const ENGINEERING_GROUP: NavGroup = {
  id: 'engineering',
  label: 'Engenharia',
  items: [
    {
      id: 'project',
      label: 'Visão geral',
      icon: <BriefcaseIcon fontSize="small" />,
      to: pathTo('project'),
      end: true
    },
    {
      id: 'harness',
      label: 'Harness',
      icon: <ShieldCheckIcon fontSize="small" />,
      to: pathTo('projectHarness'),
      tabs: [
        { id: 'harness-state', label: 'Estado e revisões', to: pathTo('projectHarness') },
        { id: 'catalog', label: 'Catálogo', to: pathTo('projectCatalog') }
      ]
    },
    { id: 'configure', label: 'Configurar', icon: <ListViewRectangleIcon fontSize="small" />, to: pathTo('configure') },
    {
      id: 'requests',
      label: 'Pedidos',
      icon: <AssignmentIcon fontSize="small" />,
      to: pathTo('engineeringRequest'),
      tabs: [
        { id: 'request', label: 'Pedido de engenharia', to: pathTo('engineeringRequest') },
        { id: 'agents', label: 'Agentes de IA', to: pathTo('agents') }
      ]
    },
    {
      id: 'knowledge',
      label: 'Conhecimento',
      icon: <BookOpenIcon fontSize="small" />,
      to: pathTo('projectKnowledge')
    },
    { id: 'security', label: 'Segurança', icon: <ShieldAlertIcon fontSize="small" />, to: pathTo('security') },
    // A infraestrutura é do projeto (os .tf dele, a nuvem dele), não do computador: fica com a Engenharia.
    {
      id: 'infrastructure',
      label: 'Infraestrutura',
      icon: <GlobeIcon fontSize="small" />,
      to: pathTo('infrastructure')
    }
  ]
};

export const MANAGEMENT_GROUP: NavGroup = {
  id: 'management',
  label: 'Gestão',
  items: [
    { id: 'portfolio', label: 'Portfólio', icon: <GridIcon fontSize="small" />, to: pathTo('portfolio') },
    {
      id: 'management',
      label: 'Acompanhamento',
      icon: <ChartBarIcon fontSize="small" />,
      to: pathTo('management'),
      end: true
    },
    { id: 'assistant', label: 'Assistente', icon: <ChatIcon fontSize="small" />, to: pathTo('assistant') },
    { id: 'report', label: 'Relatório', icon: <ReportIcon fontSize="small" />, to: pathTo('report') }
  ]
};

// Execuções e histórico servem aos dois ambientes. Conexões é tarefa de quem configura: a Gestão não a mostra no menu
// (`itemsFor`), mas a tela continua aberta para quem chegar por ela.
export const PLATFORM_GROUP: NavGroup = {
  id: 'platform',
  label: 'Plataforma',
  items: [
    { id: 'runs', label: 'Execuções', icon: <PlayIcon fontSize="small" />, to: pathTo('runs') },
    {
      id: 'history',
      label: 'Histórico',
      icon: <CalendarIcon fontSize="small" />,
      to: pathTo('timeline'),
      tabs: [
        { id: 'timeline', label: 'Linha do tempo', to: pathTo('timeline') },
        { id: 'audit', label: 'Auditoria', to: pathTo('audit') }
      ]
    },
    {
      id: 'connections',
      label: 'Conexões',
      icon: <IntersectingArrowsIcon fontSize="small" />,
      to: pathTo('integrations'),
      tabs: [
        { id: 'integrations', label: 'Serviços', to: pathTo('integrations'), keywords: 'Integrações GitHub Jira' },
        { id: 'cloud', label: 'Nuvem', to: pathTo('cloud'), keywords: 'Conexões cloud AWS Azure Google' },
        { id: 'models', label: 'Modelos de IA', to: pathTo('connectionsModels'), keywords: 'Minha IA provedores' },
        { id: 'ides', label: 'IDEs', to: pathTo('connectionsIdes'), keywords: 'Claude Code Cursor Codex MCP' }
      ]
    }
  ]
};

const MANAGEMENT_HIDDEN: ReadonlySet<string> = new Set(['connections']);

export function platformItemsFor(environment: Environment): NavItem[] {
  return PLATFORM_GROUP.items.filter((item) => environment !== 'management' || !MANAGEMENT_HIDDEN.has(item.id));
}

/** A aba da tela aberta, com o item do menu que a contém; `null` quando a tela não divide item com outras. */
export function tabOwner(pathname: string): { group: NavGroup; item: NavItem; tab: NavTab } | null {
  for (const group of [ENGINEERING_GROUP, MANAGEMENT_GROUP, PLATFORM_GROUP]) {
    for (const item of group.items) {
      const tab = item.tabs?.find((entry) => entry.to === pathname);
      if (tab) return { group, item, tab };
    }
  }
  return null;
}

export const SETTINGS_GROUP: NavGroup = {
  id: 'settings',
  label: 'Configurações',
  items: [
    { id: 'settings-ai', label: 'Minha IA', icon: <IdeaIcon fontSize="small" />, to: pathTo('settingsAi') },
    {
      id: 'settings-diagnostics',
      label: 'Diagnóstico',
      icon: <InfoIcon fontSize="small" />,
      to: pathTo('settingsDiagnostics')
    },
    { id: 'settings-about', label: 'Sobre', icon: <BookOpenIcon fontSize="small" />, to: pathTo('settingsAbout') }
  ]
};

export const ENVIRONMENT_GROUP: Record<Environment, NavGroup> = {
  engineering: ENGINEERING_GROUP,
  management: MANAGEMENT_GROUP
};
