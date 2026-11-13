export interface ComponentRequestItem {
  id: string;
  label: string;
}

/** Chega ao Configurar pelo estado da rota e só entra no plano: o motor o grava em mariliz.yaml como uma mudança que a pessoa aprova ou não. */
export interface ComponentRequest {
  exclude: ComponentRequestItem[];
  include: ComponentRequestItem[];
}

export const EMPTY_COMPONENT_REQUEST: ComponentRequest = { exclude: [], include: [] };

export function componentRequestState(request: Partial<ComponentRequest>): { componentRequest: ComponentRequest } {
  return { componentRequest: { exclude: request.exclude ?? [], include: request.include ?? [] } };
}

const isItem = (value: unknown): value is ComponentRequestItem =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as ComponentRequestItem).id === 'string' &&
  typeof (value as ComponentRequestItem).label === 'string';

const items = (value: unknown): ComponentRequestItem[] => (Array.isArray(value) ? value.filter(isItem) : []);

export function readComponentRequest(state: unknown): ComponentRequest {
  if (typeof state !== 'object' || state === null || !('componentRequest' in state)) return EMPTY_COMPONENT_REQUEST;
  const request = (state as { componentRequest: unknown }).componentRequest;
  if (typeof request !== 'object' || request === null) return EMPTY_COMPONENT_REQUEST;
  const { exclude, include } = request as Record<string, unknown>;
  return { exclude: items(exclude), include: items(include) };
}

export const hasComponentRequest = (request: ComponentRequest) => request.exclude.length + request.include.length > 0;
