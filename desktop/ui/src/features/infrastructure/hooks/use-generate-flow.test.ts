import draftFixture from '@contracts/fixtures/iac/architect-draft.json';
import optionsFixture from '@contracts/fixtures/iac/generate-options.json';
import generateFixture from '@contracts/fixtures/iac/generate-plan.json';
import { describe, expect, it } from 'vitest';

import { iacArchitectDraftSchema, iacGenerateOptionsSchema, iacGeneratePlanSchema } from '@/shared/schema';

import { generateFlow, startFlow } from './use-generate-flow';

const providers = iacGenerateOptionsSchema.parse(optionsFixture).options.providers;
const draft = iacArchitectDraftSchema.parse(draftFixture).draft;
const plan = iacGeneratePlanSchema.parse(generateFixture).plan;
const vpc = { id: 'vpc-0a1b2c3d4e5f60718', privateSubnets: ['subnet-a', 'subnet-b'], publicSubnets: ['subnet-c'] };

describe('fluxo do desenho', () => {
  it('trocar a nuvem descarta as VPCs lidas e a proposta da IA, que eram da nuvem anterior', () => {
    const withAws = generateFlow(generateFlow(startFlow(providers), { type: 'vpcs', vpcs: [vpc] }), {
      type: 'drafted',
      draft
    });

    const azure = generateFlow(withAws, { type: 'provider', options: providers[1]! });

    expect(azure.form.provider).toBe('azure');
    expect(azure.vpcs).toBeNull();
    expect(azure.draft).toBeNull();
  });

  it('a proposta da IA preenche o formulário; sem desenho legível, o formulário fica como estava', () => {
    const start = startFlow(providers);

    const proposed = generateFlow(start, { type: 'drafted', draft });
    const unreadable = generateFlow(start, { type: 'drafted', draft: { ...draft, blueprint: null } });

    expect(proposed.form.size).toBe('512/1024');
    expect(unreadable.form).toBe(start.form);
    expect(unreadable.draft?.blueprint).toBeNull();
  });

  it('escolher a VPC lida leva as subnets dela para o formulário', () => {
    const chosen = generateFlow(startFlow(providers), { type: 'vpc', vpc });

    expect(chosen.form).toMatchObject({
      vpcId: vpc.id,
      privateSubnets: 'subnet-a, subnet-b',
      publicSubnets: 'subnet-c'
    });
  });

  it('voltar da revisão descarta o plano, a estimativa e a confirmação, e mantém o desenho', () => {
    const edited = generateFlow(startFlow(providers), { type: 'edit', change: { project: 'loja-api' } });
    const reviewing = generateFlow(generateFlow(edited, { type: 'planned', plan }), {
      type: 'confirming',
      confirming: true
    });

    const back = generateFlow(reviewing, { type: 'back' });

    expect(back.plan).toBeNull();
    expect(back.estimate).toBeNull();
    expect(back.confirming).toBe(false);
    expect(back.form.project).toBe('loja-api');
  });
});
