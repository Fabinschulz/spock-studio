import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { EngineError } from '@/shared/api';
import type { Resource } from '@/shared/hooks';
import { expectNoA11yViolations } from '@/test/a11y';

import { AppProviders } from '../../../providers';
import { EmptyState, ErrorState, LoadingState, NoProjectState } from '../page-state';
import { PageSection } from '../page-section';
import { ResourceView } from '../resource-view';
import { Page } from './Page';

function renderInApp(ui: React.ReactNode) {
  return render(
    <AppProviders>
      <MemoryRouter>{ui}</MemoryRouter>
    </AppProviders>
  );
}

describe('Page', () => {
  it('tem um título de nível 1 e a frase de contexto logo abaixo', () => {
    renderInApp(
      <Page title="Portfólio" description="Os projetos abertos neste computador.">
        <p>conteúdo</p>
      </Page>
    );

    expect(screen.getByRole('heading', { level: 1, name: 'Portfólio' })).toBeInTheDocument();
    expect(screen.getByText('Os projetos abertos neste computador.')).toBeInTheDocument();
    expect(screen.getByText('conteúdo')).toBeInTheDocument();
  });

  it('aceita texto formatado na descrição, como um caminho em fonte monoespaçada', () => {
    renderInApp(<Page title="loja" description={<code>~\loja</code>} />);

    expect(screen.getByText('~\\loja').tagName).toBe('CODE');
  });
});

describe('PageSection', () => {
  it('é uma região cujo nome é o título, sem id digitado à mão', () => {
    renderInApp(
      <>
        <PageSection title="Atividades">a</PageSection>
        <PageSection title="Riscos">b</PageSection>
      </>
    );

    const activities = screen.getByRole('region', { name: 'Atividades' });
    const risks = screen.getByRole('region', { name: 'Riscos' });
    expect(activities).not.toBe(risks);
    expect(within(activities).getByRole('heading', { level: 2, name: 'Atividades' })).toBeInTheDocument();
  });

  it('mostra a descrição e a ação ao lado do título', () => {
    renderInApp(
      <PageSection title="Perfil" description="O que o projeto usa." action={<button type="button">Atualizar</button>}>
        corpo
      </PageSection>
    );

    const section = screen.getByRole('region', { name: 'Perfil' });
    expect(within(section).getByText('O que o projeto usa.')).toBeInTheDocument();
    expect(within(section).getByRole('button', { name: 'Atualizar' })).toBeInTheDocument();
  });
});

describe('Estados de página', () => {
  it('carregando é uma região de estado', () => {
    renderInApp(<LoadingState>Lendo o portfólio…</LoadingState>);

    expect(screen.getByRole('status')).toHaveTextContent('Lendo o portfólio…');
  });

  it('erro é um alerta e só oferece tentar de novo quando a leitura pode ser refeita', async () => {
    const retry = vi.fn();
    const { rerender } = renderInApp(<ErrorState>Não foi possível ler.</ErrorState>);
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível ler.');
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument();

    rerender(
      <AppProviders>
        <MemoryRouter>
          <ErrorState onRetry={retry}>Não foi possível ler.</ErrorState>
        </MemoryRouter>
      </AppProviders>
    );
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('vazio não é alerta nem carregando', () => {
    renderInApp(<EmptyState>Nenhum item ainda.</EmptyState>);

    expect(screen.getByText('Nenhum item ainda.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('sem projeto, diz onde escolher um', () => {
    renderInApp(<NoProjectState />);

    expect(screen.getByText(/Nenhum projeto selecionado/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'tela inicial' })).toHaveAttribute('href', '/');
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderInApp(
      <Page title="Tela" description="Contexto.">
        <PageSection title="Seção">
          <LoadingState>Lendo…</LoadingState>
          <ErrorState onRetry={() => undefined}>Falhou.</ErrorState>
          <NoProjectState />
        </PageSection>
      </Page>
    );

    await expectNoA11yViolations(container);
  });
});

describe('ResourceView', () => {
  const view = (resource: Resource<string>) =>
    renderInApp(
      <ResourceView resource={resource} loading="Lendo…" fallbackError="Falhou." onRetry={() => undefined}>
        {(value) => <p>pronto: {value}</p>}
      </ResourceView>
    );

  it('lendo: diz o que está lendo e não chama o conteúdo', () => {
    view({ status: 'loading' });

    expect(screen.getByRole('status')).toHaveTextContent('Lendo…');
    expect(screen.queryByText(/pronto/)).not.toBeInTheDocument();
  });

  it('pronto: entrega o valor ao conteúdo', () => {
    view({ status: 'ready', value: 'dados' });

    expect(screen.getByText('pronto: dados')).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('falhou com explicação do motor: mostra a explicação do motor', () => {
    view({ status: 'error', error: new EngineError('INTEGRATION_AUTH_FAILED', 'O provedor recusou a credencial.') });

    expect(screen.getByRole('alert')).toHaveTextContent('O provedor recusou a credencial.');
  });

  it('falhou sem explicação: mostra o texto de quem chamou, nunca o erro cru', () => {
    view({ status: 'error', error: new Error('stack trace interno') });

    expect(screen.getByRole('alert')).toHaveTextContent('Falhou.');
    expect(screen.getByRole('alert')).not.toHaveTextContent('stack trace interno');
  });
});
