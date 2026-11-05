import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RunEvalList } from './run-eval-list';

describe('RunEvalList', () => {
  it('não mostra nada enquanto o motor não anunciou nenhum Eval', () => {
    const { container } = render(<RunEvalList evals={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('uma linha por verificação, com o estado em texto; o juiz é identificado como por modelo', () => {
    render(
      <RunEvalList
        evals={[
          { id: 'EV-1', name: 'Agentes', kind: 'deterministic', status: 'passed' },
          { id: 'EV-2', name: 'Skills', kind: 'deterministic', status: 'running' },
          { id: 'EV-3', name: 'Regressão', kind: 'deterministic', status: 'blocked' },
          { id: 'EV-J', name: 'Juiz por modelo', kind: 'model', status: 'not_run' }
        ]}
      />
    );

    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(4);
    expect(rows[0]).toHaveTextContent('AgentesAprovado');
    expect(rows[1]).toHaveTextContent('Skillsem andamento');
    expect(rows[2]).toHaveTextContent('RegressãoBloqueado');
    expect(rows[3]).toHaveTextContent('por modelo');
    expect(rows[3]).toHaveTextContent('Não executado');
  });
});
