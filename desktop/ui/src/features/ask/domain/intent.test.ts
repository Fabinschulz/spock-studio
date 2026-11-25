import { describe, expect, it } from 'vitest';

import { askNotice, looksLikeRequest, rankAskOptions, suggestAskKind } from './intent';

describe('entrada de pedidos · para onde o texto vai', () => {
  it.each([
    ['Quero criar uma nova funcionalidade de login', 'request'],
    ['Verifique os problemas de arquitetura', 'request'],
    ['Como funciona a autenticação?', 'agent'],
    ['Explique o módulo de pagamentos', 'agent'],
    ['Qual é o padrão de pastas deste projeto?', 'agent'],
    ['Mostre os riscos dos meus projetos', 'assistant'],
    ['Avalie a qualidade da última entrega', 'assistant'],
    ['Quando terminamos este marco?', 'assistant'],
    ['Quando provavelmente concluiremos este projeto?', 'assistant'],
    ['Tenho uma ideia de aplicativo para agendar consultas', 'idea'],
    ['Quero começar um novo projeto do zero', 'idea']
  ])('"%s" sugere %s', (text, kind) => {
    expect(suggestAskKind(text)).toBe(kind);
  });

  it('ignora acentos e maiúsculas', () => {
    expect(suggestAskKind('PREVISÃO de conclusão')).toBe('assistant');
    expect(suggestAskKind('ONDE ESTÁ a configuração do banco')).toBe('agent');
  });

  it('sem sinal nenhum é um pedido de engenharia', () => {
    expect(suggestAskKind('trocar a cor do botão')).toBe('request');
  });

  it('uma ou duas palavras são busca, não pedido', () => {
    expect(looksLikeRequest('harness')).toBe(false);
    expect(looksLikeRequest('abrir projeto')).toBe(false);
    expect(looksLikeRequest('analise meu projeto')).toBe(true);
  });

  it('oferece todos os caminhos, o mais provável primeiro, sem repetir', () => {
    expect(rankAskOptions('Quais são os riscos?').map((option) => option.kind)).toEqual([
      'assistant',
      'request',
      'agent',
      'idea'
    ]);
    expect(rankAskOptions('trocar a cor do botão').map((option) => option.kind)).toEqual([
      'request',
      'agent',
      'assistant',
      'idea'
    ]);
  });
});

describe('entrada de pedidos · o que o caminho escolhido precisa', () => {
  const ready = { modelConfigured: true, agentInstalled: true };

  it('com tudo pronto, ou sem resposta do motor, não avisa nada', () => {
    for (const kind of ['request', 'agent', 'assistant', 'idea'] as const) {
      expect(askNotice(kind, ready)).toBeNull();
      expect(askNotice(kind, { modelConfigured: null, agentInstalled: null })).toBeNull();
    }
  });

  it('a pergunta sobre o andamento avisa da falta de modelo e diz onde conectar', () => {
    const notice = askNotice('assistant', { ...ready, modelConfigured: false });

    expect(notice?.text).toMatch(/nenhum está conectado/);
    expect(notice?.action).toEqual({ label: 'Conectar um modelo de IA', to: '/conexoes/modelos' });
  });

  it('a pergunta sobre o código avisa da falta de ferramenta instalada', () => {
    expect(askNotice('agent', { ...ready, agentInstalled: false })?.text).toMatch(/nenhuma foi encontrada/);
  });

  it('o que falta para um caminho não aparece nos outros', () => {
    const missing = { modelConfigured: false, agentInstalled: false };

    expect(askNotice('request', missing)).toBeNull();
    expect(askNotice('idea', missing)).toBeNull();
  });
});
