import { EmptyState, ErrorState, LoadingState } from '@/shared/components';
import { useEffect, useState } from 'react';

import { collapseRepeats, type LogLine } from '../domain/diagnostic-log';
import { readDiagnosticLog } from '../service/diagnostic-log-api';
import { LogList } from './diagnostic-log-section';

const POLL_MS = 2000;

/**
 * A saída do motor e do aplicativo, relida só enquanto está à vista. Relê em silêncio, sem voltar a "carregando", para a
 * lista não piscar; segredos e caminhos já chegam redigidos pelo host.
 */
export function OutputLog({ hosted, active }: { hosted: boolean; active: boolean }) {
  const [lines, setLines] = useState<LogLine[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!hosted || !active) return;
    let listening = true;
    const read = () =>
      readDiagnosticLog().then(
        (entries) => {
          if (!listening) return;
          setLines(collapseRepeats(entries));
          setFailed(false);
        },
        () => {
          if (listening) setFailed(true);
        }
      );
    void read();
    const timer = window.setInterval(() => void read(), POLL_MS);
    return () => {
      listening = false;
      window.clearInterval(timer);
    };
  }, [hosted, active]);

  if (!hosted) return <EmptyState>Sem motor local, não há saída nesta prévia.</EmptyState>;
  if (failed && lines === null) return <ErrorState>Não foi possível ler a saída do motor.</ErrorState>;
  if (lines === null) return <LoadingState>Lendo a saída…</LoadingState>;
  return <LogList lines={lines} />;
}
