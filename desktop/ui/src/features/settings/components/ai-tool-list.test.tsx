import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { aiToolListSchema } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { AiToolList } from './ai-tool-list';

const tools = aiToolListSchema.parse(aiToolsFixture).tools;

const renderList = () =>
  render(
    <AppProviders>
      <AiToolList tools={tools} />
    </AppProviders>
  );

describe('AiToolList', () => {
  it('lista cada ferramenta com o estado, a versão e o que o Spock faz com ela', () => {
    renderList();

    const claude = screen.getByText('Claude Code').closest('li')!;
    expect(within(claude).getByText('Instalada')).toBeInTheDocument();
    expect(within(claude).getByText('Versão 2.0.5.')).toBeInTheDocument();
    expect(within(claude).getByText(/O Spock configura o/)).toHaveTextContent(
      'O Spock configura o Harness para esta ferramenta.'
    );

    const cursor = screen.getByText('Cursor Agent').closest('li')!;
    expect(within(cursor).getByText('Não encontrada')).toBeInTheDocument();
    expect(within(cursor).getByText(/ainda são só do Claude Code/)).toBeInTheDocument();
    expect(within(cursor).getByRole('button', { name: 'MCP' })).toBeInTheDocument();
    const codex = screen.getByText('Codex CLI').closest('li')!;
    expect(within(codex).getByText('Instalada, mas não informou a versão.')).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderList();

    await expectNoA11yViolations(container);
  });
});
