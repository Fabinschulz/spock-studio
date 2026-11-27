import { executionPattern, isWithin, pathTo, paths } from '@/shared/routing';
import {
  AppsIcon,
  AssignmentIcon,
  BookOpenIcon,
  BriefcaseIcon,
  BroadCastIcon,
  CalendarIcon,
  ChartBarIcon,
  ChartPieIcon,
  ChatIcon,
  CheckCircleIcon,
  GlobeIcon,
  GridIcon,
  Home01Icon,
  IdeaIcon,
  InfoIcon,
  IntersectingArrowsIcon,
  LibraryIcon,
  ListViewRectangleIcon,
  MonitorIcon,
  PlayIcon,
  ReportIcon,
  SettingIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  SignalIcon
} from '@mariliz/ui-kit';
import type { ReactNode } from 'react';

/** Uma tela que divide o item do menu com outras. `keywords` guarda o nome antigo, para a busca ainda achá-la por ele. */
export interface NavTab {
  id: string;
  label: string;
  to: string;
  keywords?: string;
}

export interface NavItem {
  id: string;
  /** O nome que a pessoa lê: diz para que a tela serve. */
  label: string;
  icon: ReactNode;
  /** Uma frase sobre o que se faz ali; a busca a mostra e a usa para achar a tela. */
  description?: string;
  /** O nome técnico, quando difere do apresentado: quem o conhece ainda acha a tela por ele. */
  technical?: string;
  /** Outras palavras com que a pessoa procura a tela, que o nome e a descrição não trazem (como "documentos"). */
  keywords?: string;
  /** Sem destino = indisponível; `unavailableReason` diz por quê. */
  to?: string;
  end?: boolean;
  unavailableReason?: string;
  activity?: { label: string; text: string; tone: 'warning' | 'info' };
  /** O item fica marcado em qualquer uma delas, e a página mostra as abas para ir de uma à outra. */
  tabs?: readonly NavTab[];
}

export type AreaId = 'home' | 'engineering' | 'management' | 'activity' | 'connections' | 'settings';

/** Uma área do aplicativo: um destino na barra e, ao lado, as telas dela. O Início não tem telas próprias. */
export interface NavArea {
  id: AreaId;
  label: string;
  icon: ReactNode;
  to: string;
  description: string;
  items: NavItem[];
  /** Telas da área que não aparecem na lista (abrem a partir de outra). */
  alsoOwns?: readonly string[];
}

const EXECUTION_PREFIX = executionPattern.split('/:')[0] ?? '';

const HOME: NavArea = {
  id: 'home',
  label: 'Início',
  icon: <Home01Icon fontSize="small" />,
  to: pathTo('home'),
  description: 'Comece uma atividade ou continue de onde parou.',
  items: []
};

const ENGINEERING: NavArea = {
  id: 'engineering',
  label: 'Engenharia',
  icon: <MonitorIcon fontSize="small" />,
  to: pathTo('project'),
  description: 'O projeto aberto: padrões, pedidos, conhecimento, segurança e infraestrutura.',
  alsoOwns: [paths.idea],
  items: [
    {
      id: 'project',
      label: 'Visão geral',
      icon: <BriefcaseIcon fontSize="small" />,
      description: 'O que o Spock Studio encontrou no projeto aberto.',
      to: pathTo('project'),
      end: true
    },
    {
      id: 'harness',
      label: 'Padrões do projeto',
      technical: 'Harness',
      icon: <ShieldCheckIcon fontSize="small" />,
      description: 'Regras, verificações e assistentes de IA aplicados ao projeto.',
      to: pathTo('projectHarness'),
      tabs: [
        { id: 'harness-state', label: 'Estado e revisões', to: pathTo('projectHarness') },
        {
          id: 'catalog',
          label: 'Catálogo',
          to: pathTo('projectCatalog'),
          keywords: 'skills hooks agentes MCP assistentes capacidades'
        }
      ]
    },
    {
      id: 'configure',
      label: 'Configurar',
      icon: <ListViewRectangleIcon fontSize="small" />,
      description: 'Lê a pasta do projeto e prepara as regras e os assistentes de IA dele.',
      to: pathTo('configure')
    },
    {
      id: 'requests',
      label: 'Pedidos',
      icon: <AssignmentIcon fontSize="small" />,
      description: 'Descreva o que quer mudar e veja o impacto antes de mexer em qualquer arquivo.',
      to: pathTo('engineeringRequest'),
      tabs: [
        { id: 'request', label: 'Pedido de engenharia', to: pathTo('engineeringRequest'), keywords: 'workflow' },
        { id: 'agents', label: 'Agentes de IA', to: pathTo('agents'), keywords: 'assistentes especializados' }
      ]
    },
    {
      id: 'knowledge',
      label: 'Conhecimento',
      technical: 'RAG',
      keywords: 'documentos arquivos indexados busca',
      icon: <BookOpenIcon fontSize="small" />,
      description: 'O que os assistentes podem ler do projeto e o que fica de fora.',
      to: pathTo('projectKnowledge')
    },
    {
      id: 'security',
      label: 'Segurança',
      icon: <ShieldAlertIcon fontSize="small" />,
      description: 'Procura segredos e riscos no projeto e propõe a correção.',
      to: pathTo('security')
    },
    {
      id: 'quality',
      label: 'Revisão de código',
      keywords: 'code review alterações diff commit branch qualidade juízes',
      icon: <CheckCircleIcon fontSize="small" />,
      description: 'Revisa o que mudou no git, só leitura, com evidências e o que não pôde ser verificado.',
      to: pathTo('quality')
    },
    // A infraestrutura é do projeto (os .tf dele, a nuvem dele), não do computador: fica com a Engenharia.
    {
      id: 'infrastructure',
      label: 'Infraestrutura',
      technical: 'IaC OpenTofu Terraform',
      icon: <GlobeIcon fontSize="small" />,
      description: 'Os arquivos de infraestrutura do projeto: validar, planejar e aplicar.',
      to: pathTo('infrastructure')
    }
  ]
};

const MANAGEMENT: NavArea = {
  id: 'management',
  label: 'Gestão',
  icon: <ChartPieIcon fontSize="small" />,
  to: pathTo('management'),
  description: 'Prazos, riscos e entregas, sem precisar abrir código.',
  items: [
    {
      id: 'portfolio',
      label: 'Portfólio',
      icon: <GridIcon fontSize="small" />,
      description: 'Prazos, riscos e entregas de todos os projetos, lado a lado.',
      to: pathTo('portfolio')
    },
    {
      id: 'management',
      label: 'Acompanhamento',
      icon: <ChartBarIcon fontSize="small" />,
      description: 'O andamento do projeto aberto: atividades, riscos, marcos e decisões.',
      to: pathTo('management'),
      end: true
    },
    {
      id: 'assistant',
      label: 'Assistente',
      icon: <ChatIcon fontSize="small" />,
      description: 'Pergunte sobre andamento, riscos e previsão, de um projeto ou de vários.',
      to: pathTo('assistant')
    },
    {
      id: 'report',
      label: 'Relatório',
      icon: <ReportIcon fontSize="small" />,
      description: 'O resumo do projeto pronto para compartilhar.',
      to: pathTo('report')
    }
  ]
};

const ACTIVITY: NavArea = {
  id: 'activity',
  label: 'Atividade',
  icon: <SignalIcon fontSize="small" />,
  to: pathTo('runs'),
  description: 'O que o Spock Studio executou e o que aconteceu em cada projeto.',
  alsoOwns: [EXECUTION_PREFIX],
  items: [
    {
      id: 'runs',
      label: 'Execuções',
      icon: <PlayIcon fontSize="small" />,
      description: 'Tudo o que foi executado, o que está andando e o que espera a sua aprovação.',
      to: pathTo('runs')
    },
    {
      id: 'history',
      label: 'Histórico',
      icon: <CalendarIcon fontSize="small" />,
      description: 'A linha do tempo do projeto e a trilha de auditoria.',
      to: pathTo('timeline'),
      tabs: [
        { id: 'timeline', label: 'Linha do tempo', to: pathTo('timeline') },
        { id: 'audit', label: 'Auditoria', to: pathTo('audit') }
      ]
    }
  ]
};

const CONNECTIONS: NavArea = {
  id: 'connections',
  label: 'Conexões',
  icon: <IntersectingArrowsIcon fontSize="small" />,
  to: pathTo('integrations'),
  description: 'As ferramentas, a nuvem e os modelos de IA que o Spock Studio pode usar.',
  items: [
    {
      id: 'integrations',
      label: 'Serviços',
      icon: <AppsIcon fontSize="small" />,
      description: 'Conecte GitHub e Jira e decida o que cada projeto pode ler.',
      technical: 'Integrações GitHub Jira',
      to: pathTo('integrations')
    },
    {
      id: 'cloud',
      label: 'Nuvem',
      icon: <BroadCastIcon fontSize="small" />,
      description: 'Contas de nuvem que o projeto pode consultar.',
      technical: 'Conexões cloud AWS Azure Google',
      to: pathTo('cloud')
    },
    {
      id: 'models',
      label: 'Modelos de IA',
      icon: <LibraryIcon fontSize="small" />,
      description: 'O provedor e o modelo de IA que respondem aos pedidos.',
      technical: 'Minha IA provedores chave de API',
      to: pathTo('connectionsModels')
    }
  ]
};

const SETTINGS: NavArea = {
  id: 'settings',
  label: 'Configurações',
  icon: <SettingIcon fontSize="small" />,
  to: pathTo('settingsAi'),
  description: 'Preferências do aplicativo neste computador.',
  alsoOwns: [paths.settings],
  items: [
    {
      id: 'settings-ai',
      label: 'Modelos de IA',
      icon: <IdeaIcon fontSize="small" />,
      description: 'O provedor e o modelo de IA que respondem aos pedidos.',
      technical: 'Minha IA provedores chave de API',
      to: pathTo('settingsAi')
    },
    {
      id: 'settings-diagnostics',
      label: 'Diagnóstico',
      icon: <InfoIcon fontSize="small" />,
      description: 'O estado do motor e o registro para suporte.',
      to: pathTo('settingsDiagnostics')
    },
    {
      id: 'settings-about',
      label: 'Sobre',
      icon: <BookOpenIcon fontSize="small" />,
      description: 'Versão, licenças e o que o nome quer dizer.',
      to: pathTo('settingsAbout')
    }
  ]
};

/** Na ordem da barra. As Configurações ficam no pé dela: são preferência do aplicativo, não tela de trabalho. */
export const WORK_AREAS: readonly NavArea[] = [HOME, ENGINEERING, MANAGEMENT, ACTIVITY, CONNECTIONS];
export const SETTINGS_AREA = SETTINGS;
export const AREAS: readonly NavArea[] = [...WORK_AREAS, SETTINGS];

function prefixesOf(area: NavArea): string[] {
  const own = area.items.flatMap((item) => [item.to, ...(item.tabs ?? []).map((tab) => tab.to)]);
  return [...own, ...(area.alsoOwns ?? [])].filter((prefix): prefix is string => Boolean(prefix));
}

/** A área que contém a tela aberta, pelo prefixo mais específico; `null` numa rota que nenhuma área conhece. */
export function areaOf(pathname: string): NavArea | null {
  if (pathname === HOME.to) return HOME;
  let best: { area: NavArea; length: number } | null = null;
  for (const area of AREAS) {
    for (const prefix of prefixesOf(area)) {
      if (isWithin(pathname, prefix) && (best === null || prefix.length > best.length))
        best = { area, length: prefix.length };
    }
  }
  return best?.area ?? null;
}

/** Sem projeto aberto, o Acompanhamento não tem o que mostrar; o Portfólio cobre todos os projetos e sempre tem. */
export function areaDestination(area: NavArea, hasProject: boolean): string {
  return area.id === 'management' && !hasProject ? paths.portfolio : area.to;
}

/** A aba da tela aberta, com a área e o item que a contêm; `null` quando a tela não divide item com outras. */
export function tabOwner(pathname: string): { area: NavArea; item: NavItem; tab: NavTab } | null {
  for (const area of AREAS) {
    for (const item of area.items) {
      const tab = item.tabs?.find((entry) => entry.to === pathname);
      if (tab) return { area, item, tab };
    }
  }
  return null;
}

export interface RunActivityCount {
  running: number;
  awaitingApproval: number;
}

/**
 * O que espera a pessoa vem antes do que só está andando: com aprovação pendente o aviso mostra esse número, em tom de
 * atenção; senão, o de execuções em andamento. O nome acessível do link diz os dois.
 */
export function describeRunActivity(activity: RunActivityCount | null): NavItem['activity'] {
  if (!activity) return undefined;
  const { running, awaitingApproval } = activity;
  if (running === 0 && awaitingApproval === 0) return undefined;

  const label = [
    awaitingApproval > 0 ? `${awaitingApproval} aguardando aprovação` : null,
    running > 0 ? `${running} em andamento` : null
  ]
    .filter(Boolean)
    .join(', ');
  return awaitingApproval > 0
    ? { text: String(awaitingApproval), label, tone: 'warning' }
    : { text: String(running), label, tone: 'info' };
}
