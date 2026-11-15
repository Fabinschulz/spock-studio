import type { Tone } from '@/shared/components';
import type { TerraformInstallation, TerraformState } from '@/shared/schema';

export const TERRAFORM_STATE: Record<TerraformState, { label: string; tone: Tone }> = {
  ready: { label: 'Pronto', tone: 'success' },
  externalAvailable: { label: 'Instalado fora do Spock', tone: 'info' },
  missing: { label: 'Não instalado', tone: 'warning' },
  declined: { label: 'Instalação recusada', tone: 'neutral' },
  incompatible: { label: 'Versão incompatível', tone: 'danger' },
  unsupportedPlatform: { label: 'Sistema sem suporte', tone: 'danger' }
};

/** O texto que explica por que o Spock quer instalar o Terraform, antes de qualquer download. */
export const INSTALL_EXPLANATION = [
  'O Spock possui recursos de gerenciamento de infraestrutura utilizando Terraform (Infrastructure as Code).',
  'Com essa integração, você poderá criar, modificar, consultar e administrar infraestrutura em provedores como AWS, Azure e Google Cloud, utilizando configurações versionadas e planos de execução auditáveis.',
  'Para disponibilizar essa funcionalidade, precisamos instalar o Terraform nesta máquina.',
  'A instalação será realizada pelo próprio Spock, utilizando uma versão compatível, obtida de uma fonte oficial e validada antes da utilização.',
  'Nenhuma instalação será realizada sem sua autorização.'
] as const;

export function sourceLabel(installation: TerraformInstallation): string {
  if (installation.source === 'managed') return 'Gerenciado pelo Spock';
  return installation.adopted ? 'Instalado fora do Spock, em uso' : 'Instalado fora do Spock';
}

export type LifecycleMode = 'analysis' | 'preparation' | 'execution';

export type LifecycleStage = {
  id: string;
  label: string;
  mode: LifecycleMode;
  description: string;
  available: boolean;
};

/**
 * Discovery → Architecture → Configuration → Validation → Plan → Approval → Apply → Verification. Até o plano nada muda
 * na nuvem; a aprovação prepara a mudança; só o apply executa, e a verificação volta a só ler.
 */
export const LIFECYCLE: readonly LifecycleStage[] = [
  {
    id: 'discovery',
    label: 'Descoberta',
    mode: 'analysis',
    description: 'Lê contas e recursos pelas conexões cloud.',
    available: true
  },
  {
    id: 'architecture',
    label: 'Arquitetura',
    mode: 'analysis',
    description: 'Propõe serviços, custos e riscos.',
    available: false
  },
  {
    id: 'configuration',
    label: 'Configuração',
    mode: 'analysis',
    description: 'Gera os arquivos .tf no modelo do projeto.',
    available: false
  },
  {
    id: 'validation',
    label: 'Validação',
    mode: 'analysis',
    description: 'fmt -check e validate, sem credencial.',
    available: false
  },
  {
    id: 'plan',
    label: 'Plano',
    mode: 'analysis',
    description: 'terraform plan: mostra o que criaria, mudaria ou destruiria.',
    available: false
  },
  {
    id: 'approval',
    label: 'Aprovação',
    mode: 'preparation',
    description: 'Você aprova aquele plano específico.',
    available: false
  },
  {
    id: 'apply',
    label: 'Aplicação',
    mode: 'execution',
    description: 'Executa só o plano aprovado.',
    available: false
  },
  {
    id: 'verification',
    label: 'Verificação',
    mode: 'analysis',
    description: 'Novo plano sem mudanças e leitura dos recursos.',
    available: false
  }
];

export const LIFECYCLE_MODE: Record<LifecycleMode, { label: string; tone: Tone }> = {
  analysis: { label: 'Só analisa', tone: 'info' },
  preparation: { label: 'Prepara a mudança', tone: 'warning' },
  execution: { label: 'Muda a nuvem', tone: 'danger' }
};
