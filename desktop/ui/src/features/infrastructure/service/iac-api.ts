import type { EngineClient } from '@/shared/api';
import {
  iacRuntimeOfferResultSchema,
  iacRuntimeStatusResultSchema,
  type IacEngine,
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
      client.request('iac.runtime.pin', { projectId, ...(choice ?? {}) }, iacRuntimeStatusResultSchema).then(status)
  };
}
