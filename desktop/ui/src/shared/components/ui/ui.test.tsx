import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { expectNoA11yViolations } from '@/test/a11y';

import { ActionCard } from './molecules/action-card';
import { EvidenceDisclosure } from './molecules/evidence-disclosure';
import { PageHeader } from './molecules/page-header';
import { PrimaryActionFooter } from './molecules/primary-action-footer';
import { SectionCard } from './molecules/section-card';
import { WizardSteps } from './molecules/wizard-steps';
import { Checklist } from './atoms/checklist';
import { CommandDisplay } from './atoms/command-display';
import { InlineNotice } from './atoms/inline-notice';

const steps = [
  { id: 'a', label: 'Projeto' },
  { id: 'b', label: 'Método e ambiente' },
  { id: 'c', label: 'Plano' }
] as const;

describe('WizardSteps', () => {
  it('marca a etapa atual semanticamente e diz o estado de cada uma em texto', () => {
    render(<WizardSteps steps={steps} currentId="b" />);

    const list = screen.getByRole('list', { name: 'Etapas da configuração' });
    const [done, current, upcoming] = within(list).getAllByRole('listitem');
    expect(done).toHaveTextContent('Projeto (concluída)');
    expect(current).toHaveTextContent('Método e ambiente (etapa atual)');
    expect(current).toHaveAttribute('aria-current', 'step');
    expect(upcoming).toHaveTextContent('Plano (pendente)');
    expect(upcoming).not.toHaveAttribute('aria-current');
  });

  it('mostra o resumo "Etapa N de M" para janelas estreitas, sem duplicá-lo para o leitor de tela', () => {
    render(<WizardSteps steps={steps} currentId="b" />);

    const summary = screen.getByText(/Etapa 2 de 3/);
    expect(summary).toHaveAttribute('aria-hidden', 'true');
    expect(summary).toHaveTextContent('Método e ambiente');
  });

  it('erro e bloqueio são ditos em texto e por forma, não só por cor', () => {
    const { rerender } = render(<WizardSteps steps={steps} currentId="b" issue={{ stepId: 'b', kind: 'error' }} />);
    const item = () => within(screen.getByRole('list')).getAllByRole('listitem')[1]!;
    expect(item()).toHaveTextContent('Método e ambiente (com erro)');
    expect(item()).toHaveTextContent('!');

    rerender(<WizardSteps steps={steps} currentId="b" issue={{ stepId: 'b', kind: 'blocked' }} />);
    expect(item()).toHaveTextContent('Método e ambiente (bloqueada)');
  });

  it('não oferece navegação: etapas futuras não são controles', () => {
    render(<WizardSteps steps={steps} currentId="a" />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});

describe('ActionCard', () => {
  it('atividade disponível: botão ativo que chama a ação', async () => {
    const onClick = vi.fn();
    render(
      <ActionCard
        title="Configurar projeto"
        description="Analisar."
        icon={<span />}
        variant="primary"
        onClick={onClick}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: /Configurar projeto/ }));

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button')).not.toHaveAttribute('aria-disabled');
  });

  it('atividade indisponível: aria-disabled, motivo dito em texto e nenhuma ação', async () => {
    const onClick = vi.fn();
    render(
      <ActionCard
        title="Importar requisitos"
        description="Gerar."
        icon={<span />}
        unavailableReason="Em breve"
        onClick={onClick}
      />
    );

    const card = screen.getByRole('button', { name: /Importar requisitos/ });
    expect(card).toHaveAttribute('aria-disabled', 'true');
    expect(card).not.toBeDisabled();
    expect(card).toHaveAccessibleDescription(/Em breve: esta atividade ainda não está disponível\./);
    await userEvent.click(card);
    expect(onClick).not.toHaveBeenCalled();
  });
});

describe('PageHeader', () => {
  it('título é o h1, a trilha tem a página atual e os links levam aos destinos', () => {
    render(
      <MemoryRouter>
        <PageHeader
          title="Configurar projeto"
          description="Vamos analisar."
          breadcrumb={[{ label: 'Início', to: '/' }, { label: 'Configurar projeto' }]}
        />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { level: 1, name: 'Configurar projeto' })).toBeInTheDocument();
    const trail = screen.getByRole('navigation', { name: 'Trilha de navegação' });
    expect(within(trail).getByRole('link', { name: 'Início' })).toHaveAttribute('href', '/');
    expect(within(trail).getByText('Configurar projeto')).toHaveAttribute('aria-current', 'page');
  });
});

describe('InlineNotice', () => {
  it('só o erro é anunciado ao aparecer; informação e aviso não interrompem', () => {
    render(
      <>
        <InlineNotice tone="info">Informação</InlineNotice>
        <InlineNotice tone="warning">Aviso</InlineNotice>
        <InlineNotice tone="danger" title="Falha">
          Erro
        </InlineNotice>
      </>
    );

    expect(screen.getAllByRole('alert')).toHaveLength(1);
    expect(screen.getByRole('alert')).toHaveTextContent('Falha');
  });
});

describe('CommandDisplay', () => {
  it('mostra o comando exato, o tipo e copia para a área de transferência sem executar nada', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    render(<CommandDisplay command="dotnet build App.sln" kind="Build" />);

    expect(screen.getByText('dotnet build App.sln')).toBeInTheDocument();
    expect(screen.getByText('Build')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Copiar o comando dotnet build App.sln' }));

    expect(writeText).toHaveBeenCalledWith('dotnet build App.sln');
    expect(await screen.findByText('Comando copiado.')).toBeInTheDocument();
  });

  it('se a cópia falhar, diz como copiar à mão', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('negado')) },
      configurable: true
    });
    render(<CommandDisplay command="npm test" />);

    await userEvent.click(screen.getByRole('button', { name: /Copiar o comando npm test/ }));

    expect(await screen.findByText(/Não foi possível copiar/)).toBeInTheDocument();
  });
});

describe('Checklist', () => {
  it('o estado de cada item também é dito em texto: a marca e a cor só reforçam', () => {
    render(
      <Checklist
        label="Dependências"
        items={[
          { text: 'Node.js 25' },
          { text: 'Python 3: não encontrado', state: 'warn' },
          { text: 'Git', state: 'neutral' }
        ]}
      />
    );

    const items = within(screen.getByRole('list', { name: 'Dependências' })).getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('Node.js 25');
    expect(items[0]).not.toHaveTextContent('(atenção)');
    expect(items[1]).toHaveTextContent('Python 3: não encontrado (atenção)');
    expect(items[2]).toHaveTextContent('Git');
  });
});

describe('EvidenceDisclosure', () => {
  it('é alcançável por Tab e abre ao ativar, mostrando as evidências', async () => {
    render(<EvidenceDisclosure summary="Evidências (2)" items={['a.csproj', 'App.sln']} />);

    const summary = screen.getByText('Evidências (2)');
    const details = summary.closest('details')!;
    expect(details).not.toHaveAttribute('open');

    // O jsdom não implementa a ativação de <summary> por Enter/Espaço (isso é do navegador, conferido à mão na
    // interface); aqui se prova que o controle entra na ordem de foco e que ativá-lo abre os detalhes.
    await userEvent.tab();
    expect(summary).toHaveFocus();
    await userEvent.click(summary);

    expect(details).toHaveAttribute('open');
    expect(screen.getByText('a.csproj')).toBeInTheDocument();
  });

  it('sem itens nem conteúdo, não renderiza um controle vazio', () => {
    const { container } = render(<EvidenceDisclosure summary="Evidências (0)" items={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('acessibilidade estrutural (axe)', () => {
  it('componentes compartilhados juntos não têm violações de ARIA, nomes ou semântica', async () => {
    const { container } = render(
      <MemoryRouter>
        <PageHeader title="Título" breadcrumb={[{ label: 'Início', to: '/' }, { label: 'Atual' }]} />
        <WizardSteps steps={steps} currentId="b" />
        <h2>Título da etapa</h2>
        <SectionCard title="Cartão">
          <InlineNotice tone="warning">Aviso</InlineNotice>
          <CommandDisplay command="dotnet test" kind="Teste" />
          <EvidenceDisclosure summary="Evidências (1)" items={['x']} />
        </SectionCard>
        <ActionCard title="Ativa" description="Descrição." icon={<span />} onClick={() => undefined} />
        <ActionCard title="Em breve" description="Descrição." icon={<span />} unavailableReason="Em breve" />
        <PrimaryActionFooter>
          <button type="button">Continuar</button>
        </PrimaryActionFooter>
      </MemoryRouter>
    );

    await expectNoA11yViolations(container);
  });
});
