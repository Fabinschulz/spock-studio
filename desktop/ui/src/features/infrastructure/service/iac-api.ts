import type { EngineClient } from '@/shared/api';
import {
  iacArchitectDraftSchema,
  iacBootstrapPlanSchema,
  iacGeneratePlanSchema,
  iacGenerateSaveSchema,
  iacBootstrapSaveSchema,
  iacCostEstimateSchema,
  iacStateReadSchema,
  iacStateReportSchema,
  iacUnlockSchema,
  iacLayoutSchema,
  iacOperationListSchema,
  iacOperationResultSchema,
  iacRuntimeOfferResultSchema,
  iacRuntimeStatusResultSchema,
  type IacArchitectDraft,
  type IacBlueprint,
  type IacBootstrapPlan,
  type IacCostEstimate,
  type IacGeneratePlan,
  type IacEngine,
  type IacStateRead,
  type IacStateReport,
  type IacLayout,
  type IacOperation,
  type IacRuntimeOffer,
  type IacRuntimeSource,
  type IacRuntimeStatus
} from '@/shared/schema';

/** Cada chamada que muda algo nasce de um clique da pessoa; nenhuma é feita em segundo plano. */
export type IacApi = {
  status(projectId?: string): Promise<IacRuntimeStatus>;
  /** Sem `engine`, o motor propõe o OpenTofu (gratuito). */
  offer(input?: { version?: string; projectId?: string; engine?: IacEngine }): Promise<IacRuntimeOffer>;
  install(offer: IacRuntimeOffer): Promise<IacRuntimeStatus>;
  decline(): Promise<IacRuntimeStatus>;
  remove(engine: IacEngine, version: string): Promise<IacRuntimeStatus>;
  adopt(engine: IacEngine, version: string): Promise<IacRuntimeStatus>;
  pin(
    projectId: string,
    choice: { engine: IacEngine; version: string; source: IacRuntimeSource } | null
  ): Promise<IacRuntimeStatus>;
  layout(projectId: string): Promise<IacLayout>;
  /** fmt -check e validate sobre uma cópia da configuração: não lê state nem toca a nuvem. */
  validate(projectId: string): Promise<IacOperation>;
  /** Gera e guarda o plano; aplicar é outra etapa, com aprovação daquele plano. */
  plan(input: { projectId: string; folder?: string; workspace?: string; connectionId?: string }): Promise<IacOperation>;
  operations(projectId: string): Promise<IacOperation[]>;
  /** plan -refresh-only sobre a cópia: o que mudou na nuvem por fora da ferramenta. Não grava o state nem muda a nuvem. */
  drift(input: {
    projectId: string;
    folder?: string;
    workspace?: string;
    connectionId?: string;
  }): Promise<IacOperation>;
  /** Preços públicos do provedor pela conexão do projeto; nada da conta é lido. */
  costEstimate(input: { projectId: string; connectionId: string; blueprint: IacBlueprint }): Promise<IacCostEstimate>;
  /** Aprova aquele plano (pelo planId que a pessoa viu, com as confirmações dela) e aplica. Muda a nuvem. */
  apply(input: {
    projectId: string;
    operationId: string;
    planId: string;
    destroyConfirmed: boolean;
    risksAccepted: boolean;
    workspaceConfirmation?: string;
  }): Promise<IacOperation>;
  /** Onde vive o state de cada raiz e os riscos; lê a configuração como texto. */
  state(projectId: string): Promise<IacStateReport>;
  /** workspace list e state list sobre a cópia: endereços, nunca valores. */
  stateResources(input: {
    projectId: string;
    folder?: string;
    workspace?: string;
    connectionId?: string;
  }): Promise<IacStateRead>;
  /** force-unlock com a confirmação da pessoa. */
  unlock(input: { projectId: string; folder?: string; workspace?: string; lockId: string }): Promise<void>;
  bootstrapPlan(input: BootstrapInput): Promise<IacBootstrapPlan>;
  /** Grava os arquivos revisados como execução do Spock (desfazer pela tela da execução); devolve o id dela. */
  bootstrapSave(input: BootstrapInput & { planId: string }): Promise<string>;
  /** A IA propõe componentes e explica; os fatos (região, conta, rede, certificado) valem sobre o que ela escrever. */
  architectDraft(input: { projectId: string; need: string; facts: IacBlueprint }): Promise<IacArchitectDraft>;
  /** Confere o desenho e monta o diff dos arquivos no modelo; com checagem falhando, volta sem arquivos. */
  generatePlan(input: GenerateInput): Promise<IacGeneratePlan>;
  /** Grava os arquivos revisados como execução do Spock (desfazer pela tela da execução); devolve o id dela. */
  generateSave(input: GenerateInput & { planId: string }): Promise<string>;
};

export type GenerateInput = { projectId: string; blueprint: IacBlueprint; folder?: string };

export type BootstrapInput = {
  projectId: string;
  provider: 'aws' | 'azure' | 'gcp';
  connectionId?: string;
  values: Record<string, string>;
};

export function createIacApi(client: EngineClient): IacApi {
  const status = (result: { runtime: IacRuntimeStatus }) => result.runtime;
  return {
    status: (projectId) =>
      client.request('iac.runtime.status', projectId ? { projectId } : {}, iacRuntimeStatusResultSchema).then(status),
    offer: (input = {}) =>
      client.request('iac.runtime.offer', input, iacRuntimeOfferResultSchema).then((result) => result.offer),
    install: (offer) =>
      client
        .request(
          'iac.runtime.install',
          { offerId: offer.offerId, version: offer.version, engine: offer.engine },
          iacRuntimeStatusResultSchema
        )
        .then(status),
    decline: () => client.request('iac.runtime.decline', {}, iacRuntimeStatusResultSchema).then(status),
    remove: (engine, version) =>
      client
        .request('iac.runtime.remove', { engine, version, confirmed: true }, iacRuntimeStatusResultSchema)
        .then(status),
    adopt: (engine, version) =>
      client.request('iac.runtime.adopt', { engine, version }, iacRuntimeStatusResultSchema).then(status),
    pin: (projectId, choice) =>
      client.request('iac.runtime.pin', { projectId, ...(choice ?? {}) }, iacRuntimeStatusResultSchema).then(status),
    layout: (projectId) => client.request('iac.layout', { projectId }, iacLayoutSchema).then((r) => r.layout),
    validate: (projectId) =>
      client.request('iac.validate', { projectId }, iacOperationResultSchema).then((r) => r.operation),
    plan: (input) => client.request('iac.plan', input, iacOperationResultSchema).then((r) => r.operation),
    drift: (input) => client.request('iac.drift', input, iacOperationResultSchema).then((r) => r.operation),
    costEstimate: (input) => client.request('iac.cost.estimate', input, iacCostEstimateSchema).then((r) => r.estimate),
    state: (projectId) => client.request('iac.state', { projectId }, iacStateReportSchema).then((r) => r.state),
    stateResources: (input) => client.request('iac.state.resources', input, iacStateReadSchema).then((r) => r.state),
    unlock: async (input) => {
      await client.request('iac.state.unlock', { ...input, confirmed: true }, iacUnlockSchema);
    },
    bootstrapPlan: (input) => client.request('iac.bootstrap.plan', input, iacBootstrapPlanSchema).then((r) => r.plan),
    bootstrapSave: (input) => client.request('iac.bootstrap.save', input, iacBootstrapSaveSchema).then((r) => r.runId),
    architectDraft: (input) =>
      client.request('iac.architect.draft', input, iacArchitectDraftSchema).then((r) => r.draft),
    generatePlan: (input) => client.request('iac.generate.plan', input, iacGeneratePlanSchema).then((r) => r.plan),
    generateSave: (input) => client.request('iac.generate.save', input, iacGenerateSaveSchema).then((r) => r.runId),
    apply: (input) => client.request('iac.apply', input, iacOperationResultSchema).then((r) => r.operation),
    operations: (projectId) =>
      client.request('iac.operations', { projectId }, iacOperationListSchema).then((r) => r.operations)
  };
}
