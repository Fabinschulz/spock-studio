import type { CloudConnection, CloudRead, IacBlueprint } from '@/shared/schema';

/** O formulário guarda texto; o desenho só existe quando a pessoa pede a revisão, e o motor confere tudo de novo. */
export type BlueprintForm = {
  project: string;
  region: string;
  account: string;
  profile: string;
  vpcId: string;
  privateSubnets: string;
  publicSubnets: string;
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
};

/** As combinações de CPU e memória do Fargate oferecidas na tela; o motor aceita as demais válidas. */
export const SERVICE_SIZES = ['256/512', '512/1024', '1024/2048', '2048/4096'] as const;

export const EMPTY_FORM: BlueprintForm = {
  project: '',
  region: '',
  account: '',
  profile: '',
  vpcId: '',
  privateSubnets: '',
  publicSubnets: '',
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
  bucketRegion: ''
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
    provider: 'aws',
    project: form.project.trim(),
    region: form.region.trim(),
    account: optional(form.account),
    profile: optional(form.profile),
    network: {
      vpcId: form.vpcId.trim(),
      privateSubnets: splitList(form.privateSubnets),
      publicSubnets: splitList(form.publicSubnets)
    },
    service: form.withService
      ? {
          name: form.serviceName.trim(),
          port: integer(form.port),
          healthPath: form.healthPath.trim(),
          cpu: cpu ?? 0,
          memory: memory ?? 0,
          desiredCount: integer(form.desiredCount),
          certificateArn: form.certificateArn.trim(),
          publicIp: form.publicIp
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
      ? { bucket: form.bucket.trim(), region: form.bucketRegion.trim() || form.region.trim() }
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

/** Região, conta e perfil vêm da conexão AWS vinculada; nada disso passa pelo modelo de linguagem. */
export function fromConnection(connection: CloudConnection, current: BlueprintForm): BlueprintForm {
  return {
    ...current,
    region: connection.settings.values['region'] ?? current.region,
    profile: connection.settings.values['profile'] ?? current.profile,
    account: connection.identity?.account ?? current.account
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
