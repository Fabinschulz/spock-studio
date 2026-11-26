import { describe, expect, it } from 'vitest';

import { EngineError } from '@/shared/api';

import { describeFailure, withNextStep } from './model-errors';

describe('erros do modelo com o próximo passo', () => {
  it('uma chave recusada diz o que conferir e onde conseguir outra', () => {
    expect(withNextStep('MODEL_AUTH_FAILED', 'O provedor recusou a chave.')).toBe(
      'O provedor recusou a chave. Confira se copiou a chave inteira, sem espaços. Se ela foi apagada ou venceu, crie outra no site do provedor.'
    );
  });

  it('sem conexão aponta a internet; motor parado diz como sair', () => {
    expect(withNextStep('MODEL_UNAVAILABLE', 'O modelo não pôde ser contactado.')).toMatch(/internet/);
    expect(withNextStep('ENGINE_NOT_RUNNING', 'O motor não está em execução.')).toMatch(/feche e abra o aplicativo\.$/);
  });

  it('provedor sobrecarregado diz que a chave não é o problema', () => {
    expect(
      withNextStep('MODEL_OVERLOADED', 'O provedor está indisponível ou sobrecarregado agora (HTTP 502).')
    ).toMatch(/não na sua chave/);
  });

  it('um código sem próximo passo conhecido fica só com a mensagem do motor', () => {
    expect(withNextStep('MODEL_RATE_LIMITED', 'Tente de novo em instantes.')).toBe('Tente de novo em instantes.');
  });

  it('uma falha que não veio do motor usa o texto de reserva', () => {
    expect(describeFailure(new Error('x'), 'Não foi possível.')).toBe('Não foi possível.');
    expect(describeFailure(new EngineError('MODEL_TIMEOUT', 'O modelo não respondeu no prazo.'), '-')).toBe(
      'O modelo não respondeu no prazo. Tente de novo em instantes.'
    );
  });
});
