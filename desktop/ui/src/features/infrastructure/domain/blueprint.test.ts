import draftFixture from '@contracts/fixtures/iac/architect-draft.json';
import proposalFixture from '@contracts/fixtures/iac/architect-proposal.json';
import { describe, expect, it } from 'vitest';

import { iacArchitectDraftSchema, iacArchitectProposalSchema } from '@/shared/schema';

import { EMPTY_FORM, fromBlueprint, fromProposal, splitList, toBlueprint, withProvider } from './blueprint';

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
      { ...EMPTY_FORM, certificateArn: 'arn-antigo', publicIp: true, account: '123456789012' },
      'azure'
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

  it('o Google usa tamanhos do Cloud Run', () => {
    const google = withProvider(EMPTY_FORM, 'gcp');

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

    const form = fromProposal(proposed, { ...EMPTY_FORM, account: 'subscription-que-a-pessoa-digitou' });
    const back = toBlueprint(form);

    expect(form.provider).toBe('azure');
    expect(form.account).toBe('');
    expect(back.network).toEqual(proposed.network);
    expect(back.database).toEqual(proposed.database);
    expect(back.backend).toEqual(proposed.backend);
    expect(back.alarmEmail).toBe(proposed.alarmEmail);

    const sameCloud = fromProposal(proposed, { ...withProvider(EMPTY_FORM, 'azure'), account: 'digitada' });
    expect(sameCloud.account).toBe('digitada');
  });
});
