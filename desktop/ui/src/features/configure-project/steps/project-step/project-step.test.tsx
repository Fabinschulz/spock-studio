import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';

import { ProjectStep } from './project-step';

const project = { name: 'Loja', path: '~\\Loja' };

function renderStep(props: Partial<Parameters<typeof ProjectStep>[0]>) {
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
        <ProjectStep
          project={null}
          discovery={{ status: 'idle' }}
          onAnalyze={vi.fn()}
          onContinue={vi.fn()}
          {...props}
        />
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return within(screen.getByRole('group', { name: 'Ações da etapa' }));
}

describe('ProjectStep · por que ainda não dá para seguir', () => {
  it('sem pasta, pede a pasta', () => {
    const actions = renderStep({});

    expect(actions.getByRole('button', { name: /Próximo/ })).toBeDisabled();
    expect(actions.getByText('Escolha a pasta do projeto para continuar.')).toBeInTheDocument();
  });

  it('com pasta e sem análise, pede a análise', () => {
    const actions = renderStep({ project });

    expect(actions.getByText('Analise o projeto para continuar.')).toBeInTheDocument();
  });

  it('enquanto analisa, não repete o pedido', () => {
    const actions = renderStep({ project, discovery: { status: 'loading' } });

    expect(actions.queryByText(/para continuar/)).not.toBeInTheDocument();
  });
});
