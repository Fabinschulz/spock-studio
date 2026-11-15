import type { EngineClient } from '@/shared/api';
import {
  terraformOfferResultSchema,
  terraformStatusResultSchema,
  type TerraformOffer,
  type TerraformSource,
  type TerraformStatus
} from '@/shared/schema';

/** Cada chamada que muda algo nasce de um clique da pessoa; nenhuma é feita em segundo plano. */
export type IacApi = {
  status(projectId?: string): Promise<TerraformStatus>;
  offer(input?: { version?: string; projectId?: string }): Promise<TerraformOffer>;
  install(offer: TerraformOffer): Promise<TerraformStatus>;
  decline(): Promise<TerraformStatus>;
  remove(version: string): Promise<TerraformStatus>;
  adopt(version: string): Promise<TerraformStatus>;
  pin(projectId: string, version: string | null, source?: TerraformSource): Promise<TerraformStatus>;
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
          { offerId: offer.offerId, version: offer.version },
          terraformStatusResultSchema
        )
        .then(status),
    decline: () => client.request('iac.terraform.decline', {}, terraformStatusResultSchema).then(status),
    remove: (version) =>
      client.request('iac.terraform.remove', { version, confirmed: true }, terraformStatusResultSchema).then(status),
    adopt: (version) => client.request('iac.terraform.adopt', { version }, terraformStatusResultSchema).then(status),
    pin: (projectId, version, source) =>
      client
        .request(
          'iac.terraform.pin',
          { projectId, ...(version ? { version } : {}), ...(source ? { source } : {}) },
          terraformStatusResultSchema
        )
        .then(status)
  };
}
