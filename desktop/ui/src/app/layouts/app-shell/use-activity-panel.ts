import { useCallback, useEffect, useState } from 'react';
import { readPreference, writePreference } from '@/shared/utils';

const STORAGE_KEY = 'spock.activity';
const OPEN = 'open';
const CLOSED = 'closed';

/** Começa fechado: o painel só ocupa espaço quando a pessoa o pede, e a escolha vale para a próxima abertura. */
export function useActivityPanel() {
  const [open, setOpen] = useState(() => readPreference(STORAGE_KEY) === OPEN);
  // Conta os pedidos da pessoa: o painel que reabre sozinho ao iniciar não deve puxar o foco.
  const [focusRequest, setFocusRequest] = useState(0);

  const show = useCallback(() => {
    setOpen(true);
    setFocusRequest((value) => value + 1);
  }, []);
  const hide = useCallback(() => setOpen(false), []);

  useEffect(() => writePreference(STORAGE_KEY, open ? OPEN : CLOSED), [open]);

  return { open, focusRequest, show, hide };
}
