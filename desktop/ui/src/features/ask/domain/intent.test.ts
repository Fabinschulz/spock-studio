import { describe, expect, it } from 'vitest';

import { looksLikeRequest, rankAskOptions, suggestAskKind } from './intent';

describe('entrada de pedidos · para onde o texto vai', () => {
  it.each([
    ['Quero criar uma nova funcionalidade de login', 'request'],
    ['Verifique os problemas de arquitetura', 'request'],
    ['Mostre os riscos dos meus projetos', 'assistant'],
    ['Avalie a qualidade da última entrega', 'assistant'],
    ['Quando terminamos este marco?', 'assistant'],
    ['Tenho uma ideia de aplicativo para agendar consultas', 'idea'],
    ['Quero começar um novo projeto do zero', 'idea']
  ])('"%s" sugere %s', (text, kind) => {
    expect(suggestAskKind(text)).toBe(kind);
  });

  it('ignora acentos e maiúsculas', () => {
    expect(suggestAskKind('PREVISÃO de conclusão')).toBe('assistant');
  });

  it('sem sinal nenhum é um pedido de engenharia', () => {
    expect(suggestAskKind('trocar a cor do botão')).toBe('request');
  });

  it('uma ou duas palavras são busca, não pedido', () => {
    expect(looksLikeRequest('harness')).toBe(false);
    expect(looksLikeRequest('abrir projeto')).toBe(false);
    expect(looksLikeRequest('analise meu projeto')).toBe(true);
  });

  it('oferece os três caminhos, o mais provável primeiro, sem repetir', () => {
    const kinds = rankAskOptions('Quais são os riscos?').map((option) => option.kind);

    expect(kinds).toEqual(['assistant', 'request', 'idea']);
  });
});
