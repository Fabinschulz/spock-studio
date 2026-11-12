import { renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ContentAreaProvider } from './content-area';
import { useScrollToTop } from './use-scroll-to-top';

/** Uma área que rola e aceita foco, como o <main> do shell. O jsdom não implementa `scrollTo` em elementos. */
function contentArea() {
  const element = document.createElement('main');
  element.tabIndex = -1;
  element.scrollTo = vi.fn();
  document.body.append(element);
  return element;
}

afterEach(() => {
  document.body.replaceChildren();
});

const within =
  (area: HTMLElement) =>
  ({ children }: { children: ReactNode }) => (
    <ContentAreaProvider value={{ current: area }}>{children}</ContentAreaProvider>
  );

describe('useScrollToTop', () => {
  it('volta ao topo da área que rola, e não do documento, quando a chave muda', () => {
    const area = contentArea();
    const documentScroll = vi.fn();
    document.documentElement.scrollTo = documentScroll;
    const { rerender } = renderHook(({ key }) => useScrollToTop(key), {
      wrapper: within(area),
      initialProps: { key: 'a' }
    });
    vi.mocked(area.scrollTo).mockClear();

    rerender({ key: 'b' });

    expect(area.scrollTo).toHaveBeenCalledExactlyOnceWith({ top: 0 });
    expect(documentScroll).not.toHaveBeenCalled();
  });

  it('sem uma área informada, volta ao topo do documento', () => {
    const documentScroll = vi.fn();
    document.documentElement.scrollTo = documentScroll;

    renderHook(() => useScrollToTop('a'));

    expect(documentScroll).toHaveBeenCalledWith({ top: 0 });
  });

  it('com foco, leva o foco para a área só depois de a chave mudar, nunca ao abrir', () => {
    const area = contentArea();
    const { rerender } = renderHook(({ key }) => useScrollToTop(key, { focus: true }), {
      wrapper: within(area),
      initialProps: { key: 'a' }
    });
    expect(area).not.toHaveFocus();

    rerender({ key: 'b' });

    expect(area).toHaveFocus();
  });

  it('sem pedir foco, não mexe no foco', () => {
    const area = contentArea();
    const { rerender } = renderHook(({ key }) => useScrollToTop(key), {
      wrapper: within(area),
      initialProps: { key: 'a' }
    });

    rerender({ key: 'b' });

    expect(area).not.toHaveFocus();
  });

  it('a área pode vir por parâmetro, para quem está fora do provedor (o próprio shell)', () => {
    const area = contentArea();
    const { rerender } = renderHook(({ key }) => useScrollToTop(key, { focus: true, container: { current: area } }), {
      initialProps: { key: 'a' }
    });

    rerender({ key: 'b' });

    expect(area).toHaveFocus();
    expect(area.scrollTo).toHaveBeenCalledWith({ top: 0 });
  });
});
