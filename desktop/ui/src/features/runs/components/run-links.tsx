import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, StatusChip, type Tone } from '@/shared/components';
import type { RunItem, RunLink } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState, type FormEvent } from 'react';

import { linkRun, unlinkRun } from '../service/runs-api';
import styles from '../runs-page.module.scss';

const PROVIDER_LABEL: Record<string, string> = { github: 'GitHub', jira: 'Jira' };

const CATEGORY_TONE: Record<string, Tone> = { done: 'success', inProgress: 'info', todo: 'neutral' };

const providerLabel = (provider: string | null) =>
  provider ? (PROVIDER_LABEL[provider] ?? provider) : 'Integração removida';

/** O motor só liga o que já foi sincronizado neste projeto; ligar e desligar mexem só na ligação, não no diário. */
export function RunLinks({ client, run, initial }: { client: EngineClient; run: RunItem; initial: RunLink[] }) {
  const [links, setLinks] = useState(initial);
  const [identifier, setIdentifier] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const perform = async (action: () => Promise<RunLink[]>, failure: string): Promise<boolean> => {
    setBusy(true);
    setError(null);
    try {
      setLinks(await action());
      return true;
    } catch (cause) {
      setError(errorMessage(cause, failure));
      return false;
    } finally {
      setBusy(false);
    }
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (identifier.trim() === '') return;
    const linked = await perform(
      () => linkRun(client, run.projectId, run.runId, identifier),
      'Não foi possível ligar o item.'
    );
    if (linked) setIdentifier('');
  };

  return (
    <div className={styles.links}>
      {links.length === 0 ? (
        <p className={styles.meta}>Nenhum item de trabalho ligado a esta execução.</p>
      ) : (
        <ul className={styles.plain} aria-label="Itens ligados">
          {links.map((link) => (
            <li key={`${link.integrationId}:${link.kind}:${link.externalId}`} className={styles.linkRow}>
              <span className={styles.linkText}>
                <span className={styles.itemTitle}>#{link.externalId}</span>
                {link.available ? (
                  <span> {link.title}</span>
                ) : (
                  <span className={styles.meta}> fora do recorte atual da integração</span>
                )}
                <span className={styles.meta}> · {providerLabel(link.provider)}</span>
              </span>
              {link.available && link.status ? (
                <StatusChip tone={CATEGORY_TONE[link.statusCategory ?? ''] ?? 'neutral'}>{link.status}</StatusChip>
              ) : null}
              <Button
                variant="text"
                disabled={busy}
                aria-label={`Desligar o item ${link.externalId}`}
                onClick={() =>
                  void perform(
                    () => unlinkRun(client, run.projectId, run.runId, link),
                    'Não foi possível desligar o item.'
                  )
                }
              >
                Desligar
              </Button>
            </li>
          ))}
        </ul>
      )}
      <form className={styles.linkForm} onSubmit={(event) => void submit(event)}>
        <div className={styles.linkInput}>
          <TextField
            label="Ligar a um item (número ou chave)"
            size="small"
            fullWidth
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            placeholder="Por exemplo 123 ou APP-12"
            autoComplete="off"
            slotProps={{ htmlInput: { spellCheck: false } }}
          />
          <Button type="submit" variant="outlined" disabled={busy || identifier.trim() === ''}>
            Ligar
          </Button>
        </div>
      </form>
      {error ? <ErrorState>{error}</ErrorState> : null}
    </div>
  );
}
