import draftFixture from '@contracts/fixtures/iac/architect-draft.json';
import proposalFixture from '@contracts/fixtures/iac/architect-proposal.json';
import optionsFixture from '@contracts/fixtures/iac/generate-options.json';
import { describe, expect, it } from 'vitest';

import { iacArchitectDraftSchema, iacArchitectProposalSchema, iacGenerateOptionsSchema } from '@/shared/schema';

import {
  EMPTY_FORM,
  fromBlueprint,
  fromConnection,
  fromProposal,
  initialForm,
  splitList,
  toBlueprint,
  withProvider
} from './blueprint';

const providers = iacGenerateOptionsSchema.parse(optionsFixture).options.providers;
const cloud = (id: string) => providers.find((provider) => provider.id === id)!;

describe('desenho da infraestrutura', () => {
  it('separa listas por vírgula, espaço ou quebra de linha', () => {
    expect(splitList(' pedidos, estoque\nnotas ;; ')).toEqual(['pedidos', 'estoque', 'notas']);
  });

  it('sem componente marcado, o desenho vai sem serviço e sem banco; vazio vira null', () => {
    const blueprint = toBlueprint({ ...EMPTY_FORM, withService: false, queues: 'pedidos', bucket: '' });

    expect(blueprint.service).toBeNull();
    expect(blueprint.database).toBeNull();
    expect(blueprint.account).toBeNull();
    expect(blueprint.backend).toBeNull();
    expect(blueprint.queues).toEqual(['pedidos']);
  });

  it('o bucket de state usa a região do desenho quando a dele não é informada', () => {
    const blueprint = toBlueprint({ ...EMPTY_FORM, region: 'sa-east-1', bucket: 'loja-state' });

    expect(blueprint.backend).toEqual({ bucket: 'loja-state', region: 'sa-east-1' });
  });

  it('trocar para o Azure põe os padrões dele e leva grupo de recursos e container do state', () => {
    const azure = withProvider(
      {
        ...initialForm(providers),
        engine: 'mysql',
        certificateArn: 'arn-antigo',
        publicIp: true,
        account: '123456789012'
      },
      cloud('azure')
    );
    const blueprint = toBlueprint({
      ...azure,
      region: 'brazilsouth',
      bucket: 'lojatfstate',
      bucketResourceGroup: 'loja-tfstate',
      bucketContainer: 'tfstate',
      privateDnsZone: ' zona '
    });

    expect(azure.instanceClass).toBe('B_Standard_B1ms');
    expect(azure.storageGb).toBe('32');
    expect(azure.engine).toBe('postgres');
    expect(azure.account).toBe('');
    expect(blueprint.provider).toBe('azure');
    expect(blueprint.service?.certificateArn).toBe('');
    expect(blueprint.service?.publicIp).toBe(false);
    expect(blueprint.network.privateDnsZone).toBe('zona');
    expect(blueprint.backend).toEqual({
      bucket: 'lojatfstate',
      region: 'brazilsouth',
      resourceGroup: 'loja-tfstate',
      container: 'tfstate'
    });
  });

  it('o formulário começa com os padrões da primeira nuvem que o motor anuncia', () => {
    const form = initialForm(providers);

    expect(form).toMatchObject({ provider: 'aws', size: '256/512', instanceClass: 'db.t4g.micro', storageGb: '20' });
  });

  it('o Google usa tamanhos do Cloud Run', () => {
    const google = withProvider(initialForm(providers), cloud('gcp'));

    expect(google.size).toBe('1024/512');
    expect(toBlueprint(google).service).toMatchObject({ cpu: 1024, memory: 512 });
  });

  it('o que a IA propõe vai e volta pelo formulário sem perder tamanho, banco e filas', () => {
    const proposed = iacArchitectDraftSchema.parse(draftFixture).draft.blueprint!;

    const form = fromBlueprint(proposed, { ...EMPTY_FORM, certificateArn: 'arn-que-a-pessoa-digitou' });
    const back = toBlueprint(form);

    expect(form.size).toBe('512/1024');
    expect(back.service).toEqual(proposed.service);
    expect(back.database).toEqual(proposed.database);
    expect(back.queues).toEqual(proposed.queues);
  });

  it('o desenho do agente da sessão troca a nuvem e traz os fatos, sem apagar o que a pessoa já digitou', () => {
    const proposed = iacArchitectProposalSchema.parse(proposalFixture).proposal!.blueprint!;

    const form = fromProposal(
      proposed,
      { ...initialForm(providers), account: 'subscription-que-a-pessoa-digitou' },
      providers
    );
    const back = toBlueprint(form);

    expect(form.provider).toBe('azure');
    expect(form.account).toBe('');
    expect(back.network).toEqual(proposed.network);
    expect(back.database).toEqual(proposed.database);
    expect(back.backend).toEqual(proposed.backend);
    expect(back.alarmEmail).toBe(proposed.alarmEmail);

    const sameCloud = fromProposal(
      proposed,
      { ...withProvider(initialForm(providers), cloud('azure')), account: 'digitada' },
      providers
    );
    expect(sameCloud.account).toBe('digitada');
  });

  it('a conexão vinculada preenche conta e perfil pelas chaves de cada nuvem', () => {
    const connection = (values: Record<string, string>) =>
      ({ settings: { values }, identity: { account: 'lida-pela-conexao' } }) as unknown as Parameters<
        typeof fromConnection
      >[0];
    const aws = initialForm(providers);
    const azure = withProvider(aws, cloud('azure'));
    const google = withProvider(aws, cloud('gcp'));

    expect(fromConnection(connection({ region: 'sa-east-1', profile: 'sso' }), aws)).toMatchObject({
      region: 'sa-east-1',
      account: 'lida-pela-conexao',
      profile: 'sso'
    });
    expect(fromConnection(connection({ subscriptionId: 'sub', tenantId: 'tenant' }), azure)).toMatchObject({
      account: 'sub',
      profile: 'tenant'
    });
    expect(fromConnection(connection({ projectId: 'loja-123', profile: 'ignorado' }), google)).toMatchObject({
      account: 'loja-123',
      profile: ''
    });
  });
});
