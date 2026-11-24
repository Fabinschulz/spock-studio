import type { BlueprintForm, CloudProvider } from './blueprint';

export type TextKey = { [K in keyof BlueprintForm]: BlueprintForm[K] extends string ? K : never }[keyof BlueprintForm];

export type TextFieldSpec = { key: TextKey; label: string; hint?: string };

/**
 * Como cada nuvem chama os campos do desenho na tela e de onde a conexão vinculada tira conta e perfil. O que a nuvem
 * aceita (tamanhos, bancos, armazenamento, IA, custo) vem do motor em `iac.generate.options`; aqui fica só a apresentação.
 * Uma nuvem nova é uma linha nesta tabela.
 */
export type ProviderFields = {
  /** Conta e rede, sempre à mostra. */
  account: TextFieldSpec[];
  /** Rede que só o banco usa. */
  databaseNetwork: TextFieldSpec[];
  /** A leitura da conexão que lista VPCs e subnets da conta, quando a nuvem tem. */
  subnetDiscovery: string | null;
  /** Os valores da conexão vinculada que viram conta e perfil; sem chave de conta, vale a conta que a conexão leu. */
  connection: { account: string | null; profile: string | null };
  service: string;
  desiredCount: string;
  certificate: TextFieldSpec;
  database: string;
  instanceClass: string;
  multiAz: string;
  queues: string;
  bucket: string;
  /** O backend de state pede também grupo de recursos e container. */
  stateLocation: boolean;
};

const CERTIFICATE: TextFieldSpec = {
  key: 'certificateArn',
  label: 'Certificado ACM (ARN)',
  hint: 'Emitido na mesma região do load balancer.'
};

export const PROVIDER_FIELDS: Record<CloudProvider, ProviderFields> = {
  aws: {
    account: [
      { key: 'account', label: 'Conta (opcional)', hint: 'Vira allowed_account_ids: o provider recusa outra conta.' },
      { key: 'profile', label: 'Perfil SSO (opcional)', hint: 'Sem ele, vale a cadeia padrão de credenciais.' },
      { key: 'vpcId', label: 'VPC existente', hint: 'O Terraform não cria rede.' },
      {
        key: 'privateSubnets',
        label: 'Subnets privadas',
        hint: 'Separadas por vírgula; o banco precisa de duas zonas.'
      },
      { key: 'publicSubnets', label: 'Subnets públicas', hint: 'Para o load balancer, em duas zonas.' }
    ],
    databaseNetwork: [],
    subnetDiscovery: 'aws.ec2.subnets',
    connection: { account: null, profile: 'profile' },
    service: 'Serviço em contêiner (ECS Fargate atrás de load balancer só HTTPS)',
    desiredCount: 'Tarefas rodando',
    certificate: CERTIFICATE,
    database: 'Banco RDS (senha criada e guardada pelo próprio RDS)',
    instanceClass: 'Classe da instância',
    multiAz: 'Multi-AZ (réplica em outra zona)',
    queues: 'Filas SQS',
    bucket: 'Bucket de state (opcional)',
    stateLocation: false
  },
  azure: {
    account: [
      { key: 'account', label: 'Subscription', hint: 'GUID da subscription; o provider opera só nela.' },
      {
        key: 'profile',
        label: 'Tenant (opcional)',
        hint: 'GUID do tenant do Entra ID; sem ele, vale o da credencial.'
      }
    ],
    databaseNetwork: [
      {
        key: 'privateSubnets',
        label: 'Subnet delegada ao PostgreSQL',
        hint: 'Id completo da subnet; o Terraform não cria rede.'
      },
      {
        key: 'privateDnsZone',
        label: 'Zona DNS privada do PostgreSQL',
        hint: 'Id completo de *.postgres.database.azure.com.'
      }
    ],
    subnetDiscovery: null,
    connection: { account: 'subscriptionId', profile: 'tenantId' },
    service: 'Serviço em contêiner (Container Apps com HTTPS gerenciado)',
    desiredCount: 'Réplicas mínimas',
    certificate: CERTIFICATE,
    database: 'Banco PostgreSQL Flexible Server (só login do Entra ID, sem senha)',
    instanceClass: 'SKU',
    multiAz: 'Alta disponibilidade (réplica em outra zona)',
    queues: 'Filas do Service Bus',
    bucket: 'Storage account de state (opcional)',
    stateLocation: true
  },
  gcp: {
    account: [{ key: 'account', label: 'Projeto do Google Cloud', hint: 'Ex.: loja-producao-123.' }],
    databaseNetwork: [
      {
        key: 'vpcId',
        label: 'Rede VPC do banco',
        hint: 'projects/<projeto>/global/networks/<rede>, com acesso privado a serviços.'
      }
    ],
    subnetDiscovery: null,
    connection: { account: 'projectId', profile: null },
    service: 'Serviço em contêiner (Cloud Run com HTTPS gerenciado)',
    desiredCount: 'Réplicas mínimas',
    certificate: CERTIFICATE,
    database: 'Banco Cloud SQL (só IP privado e login IAM, sem senha)',
    instanceClass: 'Tier',
    multiAz: 'Alta disponibilidade (réplica em outra zona)',
    queues: 'Fluxos do Pub/Sub',
    bucket: 'Bucket de state (opcional)',
    stateLocation: false
  }
};

export const ENGINE_LABELS: Record<string, string> = { postgres: 'PostgreSQL', mysql: 'MySQL' };

export const isProvider = (value: string): value is CloudProvider => value in PROVIDER_FIELDS;
