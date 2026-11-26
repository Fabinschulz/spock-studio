import metricsFixture from '@contracts/fixtures/management/metrics.json';
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppProviders } from '@/shared/components';
import { projectMetricsSchema, type ProjectMetrics } from '@/shared/schema';

import { FlowSection } from './flow-section';

const metrics = projectMetricsSchema.parse(metricsFixture);

const withIssuesCycle = (cycle: Partial<ProjectMetrics['kinds'][number]['cycleTime']>): ProjectMetrics => ({
  ...metrics,
  kinds: metrics.kinds.map((flow) =>
    flow.kind === 'issues' ? { ...flow, cycleTime: { ...flow.cycleTime, ...cycle } } : flow
  )
});

const withIssuesBlocked = (blocked: Partial<ProjectMetrics['kinds'][number]['blocked']>): ProjectMetrics => ({
  ...metrics,
  kinds: metrics.kinds.map((flow) =>
    flow.kind === 'issues' ? { ...flow, blocked: { ...flow.blocked, ...blocked } } : flow
  )
});

const renderFlow = (value: ProjectMetrics) =>
  render(
    <AppProviders>
      <FlowSection metrics={value} connectors={[]} />
    </AppProviders>
  );

const issuesGroup = () => screen.getByRole('group', { name: 'issues' });

describe('Fluxo · cycle time', () => {
  it('mostra a mediana e o percentil 85 dos itens com início conhecido', () => {
    renderFlow(withIssuesCycle({ sample: 7, excluded: 0, medianDays: 4, p85Days: 6, unavailable: null }));

    const figure = within(issuesGroup()).getByRole('button', { name: 'Tempo de trabalho' }).closest('div')!;
    expect(figure).toHaveTextContent('4');
    expect(figure).toHaveTextContent('85% em até 6');
    expect(figure).not.toHaveTextContent('sem início conhecido');
  });

  it('diz quantos itens ficaram de fora por não ter início conhecido, no singular e no plural', () => {
    const { unmount } = renderFlow(
      withIssuesCycle({ sample: 6, excluded: 1, medianDays: 3, p85Days: 6, unavailable: null })
    );
    expect(within(issuesGroup()).getByRole('button', { name: 'Tempo de trabalho' }).closest('div')).toHaveTextContent(
      '1 item sem início conhecido'
    );
    unmount();

    renderFlow(withIssuesCycle({ sample: 6, excluded: 3, medianDays: 3, p85Days: 6, unavailable: null }));
    expect(within(issuesGroup()).getByRole('button', { name: 'Tempo de trabalho' }).closest('div')).toHaveTextContent(
      '3 itens sem início conhecido'
    );
  });

  it('sem valor mostra o motivo que o motor deu e não um zero', () => {
    renderFlow(
      withIssuesCycle({
        sample: 2,
        excluded: 4,
        medianDays: null,
        p85Days: null,
        unavailable:
          'São precisos ao menos 5 itens entregues com início conhecido no período; há 2 (4 sem início conhecido).'
      })
    );

    const figure = within(issuesGroup()).getByRole('button', { name: 'Tempo de trabalho' }).closest('div')!;
    expect(figure).toHaveTextContent('São precisos ao menos 5 itens entregues com início conhecido');
    expect(figure).not.toHaveTextContent(/^Tempo de trabalho, do início à entrega (mediana)\s*0/);
  });

  it('a nota de rodapé explica o que é o início e que só o Jira o informa', () => {
    renderFlow(metrics);

    expect(
      screen.getByText(/O tempo de trabalho são os\s+dias corridos do início do trabalho à conclusão/)
    ).toBeInTheDocument();
    expect(screen.getByText(/só o\s+Jira informa/)).toBeInTheDocument();
  });
});

describe('Fluxo · bloqueados', () => {
  it('conta por vínculo e por rótulo e diz o que foi lido', () => {
    renderFlow(
      withIssuesBlocked({
        value: 3,
        viaLink: 2,
        viaLabel: 2,
        linksRead: 10,
        basis: 'Vínculos lidos em 10 de 12 itens; um tipo de vínculo diferente do padrão "Blocks" não é lido.',
        unavailable: null
      })
    );

    const figure = within(issuesGroup()).getByText('Bloqueados').closest('div')!;
    expect(figure).toHaveTextContent('3');
    expect(figure).toHaveTextContent('2 por vínculo, 2 por rótulo');
    expect(figure).toHaveTextContent('Vínculos lidos em 10 de 12 itens');
  });

  it('com zero bloqueados mostra o zero e ainda diz o que foi lido', () => {
    renderFlow(
      withIssuesBlocked({
        value: 0,
        viaLink: 0,
        viaLabel: 0,
        linksRead: 0,
        basis: 'Os vínculos de bloqueio não foram lidos nesta fonte.',
        unavailable: null
      })
    );

    const figure = within(issuesGroup()).getByText('Bloqueados').closest('div')!;
    expect(figure).toHaveTextContent('0');
    expect(figure).toHaveTextContent('Os vínculos de bloqueio não foram lidos nesta fonte.');
  });

  it('sem itens mostra o motivo em vez de um número', () => {
    renderFlow(withIssuesBlocked({ value: null, unavailable: 'Ainda não há itens sincronizados deste tipo.' }));

    const figure = within(issuesGroup()).getByText('Bloqueados').closest('div')!;
    expect(figure).toHaveTextContent('Ainda não há itens sincronizados deste tipo.');
  });
});
