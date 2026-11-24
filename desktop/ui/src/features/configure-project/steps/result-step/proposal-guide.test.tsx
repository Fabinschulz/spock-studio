import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import type { HarnessNames } from '@/shared/schema';

import { ProposalGuide } from './proposal-guide';

const spock: HarnessNames = {
  declaration: 'spock.yaml',
  lock: 'spock.lock',
  stateDirectory: '.spock',
  proposal: '.spock/proposal.json',
  proposalSchema: '.spock/schemas/harness-proposal.v1.schema.json',
  harnessEngineer: 'spock-harness-engineer',
  harnessEngineerAgent: '.claude/agents/spock-harness-engineer.md'
};

const legacy: HarnessNames = {
  declaration: 'mariliz.yaml',
  lock: 'mariliz.lock',
  stateDirectory: '.mariliz',
  proposal: '.mariliz/proposal.json',
  proposalSchema: '.mariliz/schemas/harness-proposal.v1.schema.json',
  harnessEngineer: 'harness-engineer',
  harnessEngineerAgent: '.claude/agents/harness-engineer.md'
};

describe('Guia da proposta', () => {
  it('não deixa gerar outro plano enquanto uma reversão ou validação está em andamento', () => {
    const { rerender } = render(
      <AppProviders>
        <ProposalGuide onReplan={() => undefined} replan={{ status: 'idle' }} busy naming={spock} />
      </AppProviders>
    );
    expect(screen.getByRole('button', { name: 'Gerar o plano de novo' })).toBeDisabled();

    rerender(
      <AppProviders>
        <ProposalGuide onReplan={() => undefined} replan={{ status: 'idle' }} busy={false} naming={spock} />
      </AppProviders>
    );
    expect(screen.getByRole('button', { name: 'Gerar o plano de novo' })).toBeEnabled();
  });

  it('projeto configurado antes da troca de nome vê os caminhos que ele tem, não os do Spock', () => {
    render(
      <AppProviders>
        <ProposalGuide onReplan={() => undefined} replan={{ status: 'idle' }} busy={false} naming={legacy} />
      </AppProviders>
    );

    expect(screen.getByText('.claude/agents/harness-engineer.md')).toBeInTheDocument();
    expect(screen.getByText('.mariliz/proposal.json')).toBeInTheDocument();
    const prompt = (screen.getByRole('textbox', { name: 'Pedido para o Harness Engineer' }) as HTMLTextAreaElement)
      .value;
    expect(prompt).toContain('Use o agente harness-engineer ');
    expect(prompt).toContain('mariliz.yaml, mariliz.lock');
    expect(prompt).not.toContain('.spock');
  });
});
