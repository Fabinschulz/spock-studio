import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import answerFixture from '@contracts/fixtures/management/assistant-answer.json';
import { AppProviders } from '@/shared/components';
import { assistantAnswerSchema } from '@/shared/schema';

import { AssistantAnswerView } from './assistant-answer';

const answer = assistantAnswerSchema.parse(answerFixture);

const renderAnswer = (model: string) =>
  render(
    <AppProviders>
      <AssistantAnswerView answer={{ ...answer, model }} />
    </AppProviders>
  );

describe('Resposta do assistente', () => {
  it('diz qual modelo escreveu a resposta', () => {
    renderAnswer('modelo-x');

    expect(screen.getByText(/Escrito por modelo-x a partir dos/)).toHaveTextContent(
      'Escrito por modelo-x a partir dos fatos do projeto'
    );
    expect(screen.getByRole('button', { name: 'fatos' })).toHaveAccessibleDescription(
      /informação que o Spock Studio leu/
    );
  });

  it('sem modelo, não diz que um modelo escreveu', () => {
    renderAnswer('');

    expect(screen.queryByText(/Escrito por/)).not.toBeInTheDocument();
    expect(screen.getByText(/nenhum modelo de IA escreveu esta resposta/)).toBeInTheDocument();
  });
});
