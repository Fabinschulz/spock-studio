import type { CloudConnection, CloudRead, IacBlueprint } from '@/shared/schema';

/** O formulário guarda texto; o desenho só existe quando a pessoa pede a revisão, e o motor confere tudo de novo. */
export type CloudProvider = 'aws' | 'azure' | 'gcp';

export type BlueprintForm = {
  provider: CloudProvider;
  project: string;
  region: string;
  account: string;
  profile: string;
  vpcId: string;
  privateSubnets: string;
  publicSubnets: string;
  privateDnsZone: string;
  withService: boolean;
  serviceName: string;
  port: string;
  healthPath: string;
  size: string;
  desiredCount: string;
  certificateArn: string;
  publicIp: boolean;
  withDatabase: boolean;
  engine: string;
  engineVersion: string;
  instanceClass: string;
  storageGb: string;
  multiAz: boolean;
  queues: string;
  alarmEmail: string;
  bucket: string;
  bucketRegion: string;
  bucketResourceGroup: string;
  bucketContainer: string;
};

/** As combinações de CPU e memória oferecidas na tela, por nuvem; o motor aceita as demais válidas. */
export const SERVICE_SIZES: Record<CloudProvider, readonly string[]> = {
  aws: ['256/512', '512/1024', '1024/2048', '2048/4096'],
  azure: ['256/512', '512/1024', '1024/2048', '2048/4096'],
  gcp: ['1024/512', '1024/2048', '2048/4096', '4096/8192']
};

/** O que muda de uma nuvem para outra quando a pessoa troca a nuvem do desenho. */
export const PROVIDER_DEFAULTS: Record<
  CloudProvider,
  { label: string; size: string; instanceClass: string; storageGb: string; region: string }
> = {
  aws: { label: 'AWS', size: '256/512', instanceClass: 'db.t4g.micro', storageGb: '20', region: 'sa-east-1' },
  azure: { label: 'Azure', size: '256/512', instanceClass: 'B_Standard_B1ms', storageGb: '32', region: 'brazilsouth' },
  gcp: {
    label: 'Google Cloud',
    size: '1024/512',
    instanceClass: 'db-f1-micro',
    storageGb: '10',
    region: 'southamerica-east1'
  }
};

/** Troca a nuvem e põe os padrões dela no que só faz sentido naquela nuvem. */
export function withProvider(form: BlueprintForm, provider: CloudProvider): BlueprintForm {
  const defaults = PROVIDER_DEFAULTS[provider];
  return {
    ...form,
    provider,
    size: defaults.size,
    instanceClass: defaults.instanceClass,
    storageGb: defaults.storageGb,
    engine: provider === 'azure' ? 'postgres' : form.engine,
    certificateArn: provider === 'aws' ? form.certificateArn : '',
    publicIp: provider === 'aws' ? form.publicIp : false,
    account: '',
    profile: '',
    vpcId: '',
    privateSubnets: '',
    publicSubnets: '',
    privateDnsZone: ''
  };
}

export const EMPTY_FORM: BlueprintForm = {
  provider: 'aws',
  project: '',
  region: '',
  account: '',
  profile: '',
  vpcId: '',
  privateSubnets: '',
  publicSubnets: '',
  privateDnsZone: '',
  withService: true,
  serviceName: 'api',
  port: '8080',
  healthPath: '/health',
  size: '256/512',
  desiredCount: '1',
  certificateArn: '',
  publicIp: false,
  withDatabase: false,
  engine: 'postgres',
  engineVersion: '16',
  instanceClass: 'db.t4g.micro',
  storageGb: '20',
  multiAz: false,
  queues: '',
  alarmEmail: '',
  bucket: '',
  bucketRegion: '',
  bucketResourceGroup: '',
  bucketContainer: ''
};

/** Vírgula, espaço ou quebra de linha separam os itens. */
export function splitList(text: string): string[] {
  return text
    .split(/[\s,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

const optional = (text: string) => (text.trim().length > 0 ? text.trim() : null);

const integer = (text: string) => {
  const value = Number.parseInt(text, 10);
  return Number.isNaN(value) ? 0 : value;
};

export function toBlueprint(form: BlueprintForm): IacBlueprint {
  const [cpu, memory] = form.size.split('/').map(integer);
  return {
    provider: form.provider,
    project: form.project.trim(),
    region: form.region.trim(),
    account: optional(form.account),
    profile: optional(form.profile),
    network: {
      vpcId: form.vpcId.trim(),
      privateSubnets: splitList(form.privateSubnets),
      publicSubnets: splitList(form.publicSubnets),
      privateDnsZone: optional(form.privateDnsZone)
    },
    service: form.withService
      ? {
          name: form.serviceName.trim(),
          port: integer(form.port),
          healthPath: form.healthPath.trim(),
          cpu: cpu ?? 0,
          memory: memory ?? 0,
          desiredCount: integer(form.desiredCount),
          certificateArn: form.provider === 'aws' ? form.certificateArn.trim() : '',
          publicIp: form.provider === 'aws' && form.publicIp
        }
      : null,
    database: form.withDatabase
      ? {
          engine: form.engine,
          engineVersion: form.engineVersion.trim(),
          instanceClass: form.instanceClass.trim(),
          storageGb: integer(form.storageGb),
          multiAz: form.multiAz
        }
      : null,
    queues: splitList(form.queues),
    alarmEmail: optional(form.alarmEmail),
    backend: optional(form.bucket)
      ? {
          bucket: form.bucket.trim(),
          region: form.bucketRegion.trim() || form.region.trim(),
          ...(form.provider === 'azure'
            ? { resourceGroup: optional(form.bucketResourceGroup), container: optional(form.bucketContainer) }
            : {})
        }
      : null
  };
}

/** O que a IA propôs entra no formulário; os campos que a pessoa já preencheu e a IA não traz ficam como estavam. */
export function fromBlueprint(blueprint: IacBlueprint, current: BlueprintForm): BlueprintForm {
  const service = blueprint.service;
  const database = blueprint.database;
  return {
    ...current,
    project: blueprint.project || current.project,
    region: blueprint.region || current.region,
    withService: service !== null,
    ...(service
      ? {
          serviceName: service.name,
          port: String(service.port),
          healthPath: service.healthPath,
          size: `${service.cpu}/${service.memory}`,
          desiredCount: String(service.desiredCount),
          publicIp: service.publicIp,
          certificateArn: service.certificateArn || current.certificateArn
        }
      : {}),
    withDatabase: database !== null,
    ...(database
      ? {
          engine: database.engine,
          engineVersion: database.engineVersion,
          instanceClass: database.instanceClass,
          storageGb: String(database.storageGb),
          multiAz: database.multiAz
        }
      : {}),
    queues: blueprint.queues.join(', ')
  };
}

const isProvider = (value: string): value is CloudProvider => value in PROVIDER_DEFAULTS;

const filled = (value: string | null | undefined, fallback: string) => (value ? value : fallback);

const filledList = (values: string[], fallback: string) => (values.length > 0 ? values.join(', ') : fallback);

/**
 * O desenho que o agente da sessão gravou no projeto. Diferente da proposta da IA do motor, o agente pode trazer os fatos
 * da conta, porque os perguntou à pessoa ou leu da conexão; o que vier vazio fica como a pessoa preencheu. A nuvem do
 * agente vale, e o motor confere tudo de novo na revisão.
 */
export function fromProposal(blueprint: IacBlueprint, current: BlueprintForm): BlueprintForm {
  const provider = isProvider(blueprint.provider) ? blueprint.provider : current.provider;
  const form = fromBlueprint(blueprint, provider === current.provider ? current : withProvider(current, provider));
  return {
    ...form,
    account: filled(blueprint.account, form.account),
    profile: filled(blueprint.profile, form.profile),
    vpcId: filled(blueprint.network.vpcId, form.vpcId),
    privateSubnets: filledList(blueprint.network.privateSubnets, form.privateSubnets),
    publicSubnets: filledList(blueprint.network.publicSubnets, form.publicSubnets),
    privateDnsZone: filled(blueprint.network.privateDnsZone, form.privateDnsZone),
    alarmEmail: filled(blueprint.alarmEmail, form.alarmEmail),
    bucket: filled(blueprint.backend?.bucket, form.bucket),
    bucketRegion: filled(blueprint.backend?.region, form.bucketRegion),
    bucketResourceGroup: filled(blueprint.backend?.resourceGroup, form.bucketResourceGroup),
    bucketContainer: filled(blueprint.backend?.container, form.bucketContainer)
  };
}

/**
 * Região e conta vêm da conexão vinculada da mesma nuvem: perfil na AWS, subscription e tenant no Azure, projeto no
 * Google. Nada disso passa pelo modelo de linguagem.
 */
export function fromConnection(connection: CloudConnection, current: BlueprintForm): BlueprintForm {
  const values = connection.settings.values;
  const account =
    current.provider === 'azure'
      ? (values['subscriptionId'] ?? connection.identity?.account)
      : current.provider === 'gcp'
        ? (values['projectId'] ?? connection.identity?.account)
        : connection.identity?.account;
  return {
    ...current,
    region: values['region'] ?? current.region,
    profile:
      (current.provider === 'azure'
        ? values['tenantId']
        : current.provider === 'aws'
          ? values['profile']
          : undefined) ?? current.profile,
    account: account ?? current.account
  };
}

export type DiscoveredVpc = { id: string; privateSubnets: string[]; publicSubnets: string[] };

/** Agrupa a leitura aws.ec2.subnets por VPC, separando as subnets que saem por internet gateway (públicas). */
export function vpcsFromSubnets(read: CloudRead): DiscoveredVpc[] {
  const column = (id: string) => read.columns.findIndex((candidate) => candidate.id === id);
  const [idAt, vpcAt, publicAt] = [column('id'), column('vpc'), column('public')];
  const vpcs = new Map<string, DiscoveredVpc>();
  for (const row of read.rows) {
    const id = row[idAt];
    const vpc = row[vpcAt];
    if (!id || !vpc) continue;
    const entry = vpcs.get(vpc) ?? { id: vpc, privateSubnets: [], publicSubnets: [] };
    (row[publicAt] === 'sim' ? entry.publicSubnets : entry.privateSubnets).push(id);
    vpcs.set(vpc, entry);
  }
  return [...vpcs.values()];
}
