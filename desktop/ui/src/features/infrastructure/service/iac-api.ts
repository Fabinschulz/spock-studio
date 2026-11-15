import type { EngineClient } from '@/shared/api';
import {
  terraformOfferResultSchema,
  terraformStatusResultSchema,
  type IacEngine,
  type TerraformOffer,
  type TerraformSource,
  type TerraformStatus
} from '@/shared/schema';

/** Cada chamada que muda algo nasce de um clique da pessoa; nenhuma é feita em segundo plano. */
export type IacApi = {
  status(projectId?: string): Promise<TerraformStatus>;
  /** Sem `engine`, o motor propõe o OpenTofu (gratuito). */
  offer(input?: { version?: string; projectId?: string; engine?: IacEngine }): Promise<TerraformOffer>;
  install(offer: TerraformOffer): Promise<TerraformStatus>;
  decline(): Promise<TerraformStatus>;
  remove(engine: IacEngine, version: string): Promise<TerraformStatus>;
  adopt(engine: IacEngine, version: string): Promise<TerraformStatus>;
  pin(
    projectId: string,
    choice: { engine: IacEngine; version: string; source: TerraformSource } | null
  ): Promise<TerraformStatus>;
};

export function createIacApi(client: EngineClient): IacApi {
  const status = (result: { terraform: TerraformStatus }) => result.terraform;
  return {
    status: (projectId) =>
      client.request('iac.terraform.status', projectId ? { projectId } : {}, terraformStatusResultSchema).then(status),
    offer: (input = {}) =>
      client.request('iac.terraform.offer', input, terraformOfferResultSchema).then((result) => result.offer),
    install: (offer) =>
      client
        .request(
          'iac.terraform.install',
          { offerId: offer.offerId, version: offer.version, engine: offer.engine },
          terraformStatusResultSchema
        )
        .then(status),
    decline: () => client.request('iac.terraform.decline', {}, terraformStatusResultSchema).then(status),
    remove: (engine, version) =>
      client
        .request('iac.terraform.remove', { engine, version, confirmed: true }, terraformStatusResultSchema)
        .then(status),
    adopt: (engine, version) =>
      client.request('iac.terraform.adopt', { engine, version }, terraformStatusResultSchema).then(status),
    pin: (projectId, choice) =>
      client.request('iac.terraform.pin', { projectId, ...(choice ?? {}) }, terraformStatusResultSchema).then(status)
  };
}
