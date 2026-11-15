import type { EngineClient } from '@/shared/api';
import {
  iacLayoutSchema,
  iacOperationListSchema,
  iacOperationResultSchema,
  iacRuntimeOfferResultSchema,
  iacRuntimeStatusResultSchema,
  type IacEngine,
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
  /** Aprova aquele plano (pelo planId que a pessoa viu, com as confirmações dela) e aplica. Muda a nuvem. */
  apply(input: {
    projectId: string;
    operationId: string;
    planId: string;
    destroyConfirmed: boolean;
    risksAccepted: boolean;
    workspaceConfirmation?: string;
  }): Promise<IacOperation>;
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
    apply: (input) => client.request('iac.apply', input, iacOperationResultSchema).then((r) => r.operation),
    operations: (projectId) =>
      client.request('iac.operations', { projectId }, iacOperationListSchema).then((r) => r.operations)
  };
}
