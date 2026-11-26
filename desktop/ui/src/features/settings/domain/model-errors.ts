import { EngineError } from '@/shared/api';

const ENGINE_DOWN = 'Tente de novo em instantes; se continuar, feche e abra o aplicativo.';

// O motor diz o que houve; a pessoa precisa saber o que fazer em seguida.
const NEXT_STEP: Record<string, string> = {
  MODEL_AUTH_FAILED:
    'Confira se copiou a chave inteira, sem espaços. Se ela foi apagada ou venceu, crie outra no site do provedor.',
  MODEL_UNAVAILABLE: 'Confira a conexão com a internet, ou o proxy da empresa, e tente de novo.',
  MODEL_OVERLOADED: 'Tente de novo em instantes; se continuar, o problema está no provedor ou no gateway, não na sua chave.',
  MODEL_TIMEOUT: 'Tente de novo em instantes.',
  ENGINE_NOT_RUNNING: ENGINE_DOWN,
  ENGINE_STOPPED: ENGINE_DOWN,
  ENGINE_WRITE_FAILED: ENGINE_DOWN,
  ENGINE_TIMEOUT: ENGINE_DOWN
};

export function withNextStep(code: string, message: string): string {
  const next = NEXT_STEP[code];
  return next ? `${message} ${next}` : message;
}

export function describeFailure(cause: unknown, fallback: string): string {
  return cause instanceof EngineError ? withNextStep(cause.code, cause.message) : fallback;
}
