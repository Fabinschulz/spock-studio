import { describe, expect, it } from 'vitest';

import { trailOf } from './trail';

describe('trailOf', () => {
  it('o início não tem trilha', () => {
    expect(trailOf('/')).toEqual([]);
  });

  it('uma tela do menu mostra a área, que leva à tela inicial dela, e o nome da tela', () => {
    expect(trailOf('/projeto/conhecimento')).toEqual([
      { label: 'Engenharia', to: '/projeto' },
      { label: 'Conhecimento' }
    ]);
    expect(trailOf('/gestao/relatorio')).toEqual([{ label: 'Gestão', to: '/gestao' }, { label: 'Relatório' }]);
  });

  it('uma aba mostra o item do menu que a contém e o nome da aba', () => {
    expect(trailOf('/projeto/catalogo')).toEqual([
      { label: 'Engenharia', to: '/projeto' },
      { label: 'Padrões do projeto', to: '/projeto/harness' },
      { label: 'Catálogo' }
    ]);
    expect(trailOf('/auditoria')).toEqual([
      { label: 'Atividade', to: '/execucoes' },
      { label: 'Histórico', to: '/linha-do-tempo' },
      { label: 'Auditoria' }
    ]);
  });

  it('Atividade e Conexões são áreas com tela própria', () => {
    expect(trailOf('/execucoes')).toEqual([{ label: 'Atividade', to: '/execucoes' }, { label: 'Execuções' }]);
    expect(trailOf('/cloud')).toEqual([{ label: 'Conexões', to: '/integracoes' }, { label: 'Nuvem' }]);
  });

  it('as Configurações têm trilha própria, que leva à primeira seção', () => {
    expect(trailOf('/configuracoes/diagnostico')).toEqual([
      { label: 'Configurações', to: '/configuracoes/ia' },
      { label: 'Diagnóstico' }
    ]);
  });

  it('a execução passa pela lista de execuções', () => {
    expect(trailOf('/execucao/run-1')).toEqual([
      { label: 'Atividade', to: '/execucoes' },
      { label: 'Execuções', to: '/execucoes' },
      { label: 'Execução' }
    ]);
  });

  it('uma rota que não está no menu fica sem trilha', () => {
    expect(trailOf('/nao-existe')).toEqual([]);
  });
});
