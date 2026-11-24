import type { CloudApi } from '@/features/cloud';
import { useAction } from '@/shared/hooks';
import type {
  CloudConnection,
  IacArchitectDraft,
  IacBlueprint,
  IacCostEstimate,
  IacGeneratePlan,
  IacProviderOptions
} from '@/shared/schema';
import { useReducer } from 'react';

import {
  fromBlueprint,
  fromConnection,
  fromProposal,
  initialForm,
  toBlueprint,
  vpcsFromSubnets,
  withProvider,
  type BlueprintForm,
  type DiscoveredVpc
} from '../domain/blueprint';
import type { IacApi } from '../service/iac-api';

export type GenerateFlowState = {
  form: BlueprintForm;
  need: string;
  draft: IacArchitectDraft | null;
  vpcs: DiscoveredVpc[] | null;
  plan: IacGeneratePlan | null;
  estimate: IacCostEstimate | null;
  savedRun: string | null;
};

export type GenerateFlowEvent =
  | { type: 'edit'; change: Partial<BlueprintForm> }
  | { type: 'need'; need: string }
  | { type: 'provider'; options: IacProviderOptions }
  | { type: 'proposal'; blueprint: IacBlueprint; providers: IacProviderOptions[] }
  | { type: 'connection'; connection: CloudConnection }
  | { type: 'drafted'; draft: IacArchitectDraft }
  | { type: 'vpcs'; vpcs: DiscoveredVpc[] }
  | { type: 'vpc'; vpc: DiscoveredVpc }
  | { type: 'planned'; plan: IacGeneratePlan }
  | { type: 'estimated'; estimate: IacCostEstimate }
  | { type: 'saved'; runId: string }
  | { type: 'back' };

export const startFlow = (providers: IacProviderOptions[]): GenerateFlowState => ({
  form: initialForm(providers),
  need: '',
  draft: null,
  vpcs: null,
  plan: null,
  estimate: null,
  savedRun: null
});

/**
 * As três telas do desenho (formulário, revisão e gravado) num estado só. Trocar a nuvem ou usar o desenho do agente
 * descarta as VPCs lidas e a proposta da IA, que eram da nuvem anterior; voltar da revisão descarta o plano e a
 * estimativa, que eram daquele desenho.
 */
export function generateFlow(state: GenerateFlowState, event: GenerateFlowEvent): GenerateFlowState {
  switch (event.type) {
    case 'edit':
      return { ...state, form: { ...state.form, ...event.change } };
    case 'need':
      return { ...state, need: event.need };
    case 'provider':
      return { ...state, form: withProvider(state.form, event.options), vpcs: null, draft: null };
    case 'proposal':
      return { ...state, form: fromProposal(event.blueprint, state.form, event.providers), vpcs: null, draft: null };
    case 'connection':
      return { ...state, form: fromConnection(event.connection, state.form) };
    case 'drafted':
      return {
        ...state,
        draft: event.draft,
        form: event.draft.blueprint ? fromBlueprint(event.draft.blueprint, state.form) : state.form
      };
    case 'vpcs':
      return { ...state, vpcs: event.vpcs };
    case 'vpc':
      return {
        ...state,
        form: {
          ...state.form,
          vpcId: event.vpc.id,
          privateSubnets: event.vpc.privateSubnets.join(', '),
          publicSubnets: event.vpc.publicSubnets.join(', ')
        }
      };
    case 'planned':
      return { ...state, plan: event.plan };
    case 'estimated':
      return { ...state, estimate: event.estimate };
    case 'saved':
      return { ...state, savedRun: event.runId };
    case 'back':
      return { ...state, plan: null, estimate: null };
  }
}

// Nada neste fluxo muda o que a página já mostra: o resultado de cada ação fica no próprio painel.
const NOTHING = () => undefined;

interface GenerateFlowInput {
  api: IacApi;
  cloud: CloudApi;
  projectId: string;
  providers: IacProviderOptions[];
}

/** O estado do desenho e as chamadas ao motor que o fazem andar; quem desenha a tela só lê e dispara. */
export function useGenerateFlow({ api, cloud, projectId, providers }: GenerateFlowInput) {
  const [state, dispatch] = useReducer(generateFlow, providers, startFlow);
  const action = useAction(NOTHING);
  const options = providers.find((candidate) => candidate.id === state.form.provider) ?? providers[0]!;
  const blueprint = () => toBlueprint(state.form);

  return {
    state,
    dispatch,
    options,
    busy: action.busy,
    error: action.error,
    propose: () =>
      action.run(
        async () =>
          dispatch({
            type: 'drafted',
            draft: await api.architectDraft({ projectId, need: state.need, facts: blueprint() })
          }),
        'A IA não conseguiu propor o desenho. Monte à mão abaixo.'
      ),
    readVpcs: (connection: CloudConnection, capability: string) =>
      action.run(async () => {
        const read = await cloud.read({
          connectionId: connection.id,
          capability,
          region: state.form.region.trim(),
          limit: 200
        });
        dispatch({ type: 'vpcs', vpcs: vpcsFromSubnets(read) });
      }, 'Não foi possível ler as subnets pela conexão.'),
    review: () =>
      action.run(
        async () => dispatch({ type: 'planned', plan: await api.generatePlan({ projectId, blueprint: blueprint() }) }),
        'Não foi possível montar os arquivos.'
      ),
    estimate: (connection: CloudConnection) =>
      action.run(
        async () =>
          dispatch({
            type: 'estimated',
            estimate: await api.costEstimate({ projectId, connectionId: connection.id, blueprint: blueprint() })
          }),
        'Não foi possível consultar os preços pela conexão.'
      ),
    save: (planId: string) =>
      action.run(
        async () =>
          dispatch({ type: 'saved', runId: await api.generateSave({ projectId, blueprint: blueprint(), planId }) }),
        'Os arquivos não foram gravados.'
      )
  };
}

export type GenerateFlow = ReturnType<typeof useGenerateFlow>;
