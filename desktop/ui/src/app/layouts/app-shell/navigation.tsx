import { pathTo } from '@/shared/routing';
import {
  AssignmentIcon,
  BookOpenIcon,
  BriefcaseIcon,
  CalendarIcon,
  ChartBarIcon,
  ChatIcon,
  ComplianceIcon,
  GridIcon,
  Home01Icon,
  IntersectingArrowsIcon,
  LibraryIcon,
  ListViewRectangleIcon,
  PlayIcon,
  ReportIcon,
  SettingIcon,
  ShieldCheckIcon
} from '@mariliz/ui-kit';
import type { ReactNode } from 'react';
import type { Environment } from './environment';
export interface NavItem {
  id: string;
  label: string;
  icon: ReactNode;
  /** Sem destino = indisponível; `unavailableReason` diz por quê. */
  to?: string;
  end?: boolean;
  unavailableReason?: string;
  activity?: { label: string; text: string; tone: 'warning' | 'info' };
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
    { id: 'harness', label: 'Harness', icon: <ShieldCheckIcon fontSize="small" />, to: pathTo('projectHarness') },
    {
      id: 'knowledge',
      label: 'Conhecimento',
      icon: <BookOpenIcon fontSize="small" />,
      to: pathTo('projectKnowledge')
    },
    { id: 'configure', label: 'Configurar', icon: <ListViewRectangleIcon fontSize="small" />, to: pathTo('configure') },
    {
      id: 'request',
      label: 'Pedido de engenharia',
      icon: <AssignmentIcon fontSize="small" />,
      to: pathTo('engineeringRequest')
    },
    { id: 'catalog', label: 'Catálogo', icon: <LibraryIcon fontSize="small" />, to: pathTo('projectCatalog') }
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

// Integrações alimenta os dois ambientes e por isso não pertence a nenhum: aparece sempre.
export const PLATFORM_GROUP: NavGroup = {
  id: 'platform',
  label: 'Plataforma',
  items: [
    { id: 'runs', label: 'Execuções', icon: <PlayIcon fontSize="small" />, to: pathTo('runs') },
    { id: 'timeline', label: 'Linha do tempo', icon: <CalendarIcon fontSize="small" />, to: pathTo('timeline') },
    { id: 'audit', label: 'Auditoria', icon: <ComplianceIcon fontSize="small" />, to: pathTo('audit') },
    {
      id: 'integrations',
      label: 'Integrações',
      icon: <IntersectingArrowsIcon fontSize="small" />,
      to: pathTo('integrations')
    },
    { id: 'settings', label: 'Configurações', icon: <SettingIcon fontSize="small" />, to: pathTo('settings') }
  ]
};

export const ENVIRONMENT_GROUP: Record<Environment, NavGroup> = {
  engineering: ENGINEERING_GROUP,
  management: MANAGEMENT_GROUP
};
