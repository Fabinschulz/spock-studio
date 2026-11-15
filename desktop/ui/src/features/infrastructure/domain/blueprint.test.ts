import draftFixture from '@contracts/fixtures/iac/architect-draft.json';
import { describe, expect, it } from 'vitest';

import { iacArchitectDraftSchema } from '@/shared/schema';

import { EMPTY_FORM, fromBlueprint, splitList, toBlueprint } from './blueprint';

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

  it('o que a IA propõe vai e volta pelo formulário sem perder tamanho, banco e filas', () => {
    const proposed = iacArchitectDraftSchema.parse(draftFixture).draft.blueprint!;

    const form = fromBlueprint(proposed, { ...EMPTY_FORM, certificateArn: 'arn-que-a-pessoa-digitou' });
    const back = toBlueprint(form);

    expect(form.size).toBe('512/1024');
    expect(back.service).toEqual(proposed.service);
    expect(back.database).toEqual(proposed.database);
    expect(back.queues).toEqual(proposed.queues);
  });
});
