import { looksLikeRequest, rankAskOptions } from '@/features/ask';
import { useProjectSelectionContext } from '@/features/project-selection';
import { handoffState } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Modal, TextField } from '@mariliz/ui-kit';
import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router';

import { buildCommands, type Command, type ShellAction } from './commands';
import styles from './CommandPalette.module.scss';
import { searchCommands, suggestCommands } from './search-commands';

const ASK_GROUP = 'Pedir ao Spock Studio';

/** Uma linha da lista: um comando conhecido ou um caminho para o texto que a pessoa escreveu. */
interface Entry {
  id: string;
  group: string;
  label: string;
  hint: string;
  description?: string;
  run: () => void;
}

interface CommandPaletteProps {
  open: boolean;
  onClose: () => void;
  onShellAction: (action: ShellAction) => void;
}

export function CommandPalette({ open, onClose, onShellAction }: CommandPaletteProps) {
  return (
    <Modal open={open} onClose={onClose} title="Buscar ou pedir" closeLabel="Fechar" sizeModal="medium">
      <PaletteBody onClose={onClose} onShellAction={onShellAction} />
    </Modal>
  );
}

function PaletteBody({ onClose, onShellAction }: Omit<CommandPaletteProps, 'open'>) {
  const listId = useId();
  const navigate = useNavigate();
  const { selected, recents, openRecent, pick } = useProjectSelectionContext();
  const commands = useMemo(() => buildCommands(recents), [recents]);
  const field = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);

  useEffect(() => {
    field.current?.focus();
  }, []);

  const entries = useMemo<Entry[]>(() => {
    const text = query.trim();
    const sentence = looksLikeRequest(text);

    const runCommand = ({ action }: Command) => {
      if (action.kind === 'go') navigate(action.to);
      else if (action.kind === 'open-project') void openRecent(action.recentId);
      else if (action.kind === 'pick-project') void pick();
      else onShellAction(action.action);
    };
    // O pedido precisa de um projeto: sem um aberto, a pessoa escolhe a pasta antes de seguir.
    const ask = async (to: string) => {
      if (selected ?? (await pick())) navigate(to, { state: handoffState(text) });
    };

    const toEntry = (command: Command): Entry => ({
      id: command.id,
      group: command.group,
      label: command.label,
      hint: command.hint,
      description: command.description,
      run: () => runCommand(command)
    });
    const asks: Entry[] = sentence
      ? rankAskOptions(text).map((option) => ({
          id: `ask:${option.kind}`,
          group: ASK_GROUP,
          label: option.label,
          hint: 'com o texto acima',
          description: option.description,
          run: () => void ask(option.to)
        }))
      : [];

    // Quem escreveu o nome de algo quer esse algo; quem escreveu uma frase quer pedir, e as telas só complementam.
    const exact = searchCommands(commands, text);
    const list =
      exact.length > 0 || !sentence
        ? [...exact.map(toEntry), ...asks]
        : [...asks, ...suggestCommands(commands, text).map(toEntry)];
    // A lista segue a ordem em que aparece na tela, grupo a grupo: as setas andam por ela sem saltar.
    const order = [...new Set(list.map((entry) => entry.group))];
    return order.flatMap((group) => list.filter((entry) => entry.group === group));
  }, [commands, query, selected, navigate, openRecent, pick, onShellAction]);

  const groups = useMemo(() => {
    const byGroup = new Map<string, { entry: Entry; index: number }[]>();
    entries.forEach((entry, index) =>
      byGroup.set(entry.group, [...(byGroup.get(entry.group) ?? []), { entry, index }])
    );
    return [...byGroup];
  }, [entries]);

  const current = Math.min(active, entries.length - 1);
  const optionId = (index: number) => `${listId}-${index}`;

  const run = (entry: Entry) => {
    onClose();
    entry.run();
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (entries.length === 0) return;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive((current + step + entries.length) % entries.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      run(entries[current]!);
    }
  };

  return (
    <div className={styles.palette}>
      <TextField
        label="Tela, projeto, comando ou pedido"
        placeholder="Ex.: conexões, ou: verifique o impacto de trocar a autenticação"
        fullWidth
        inputRef={field}
        autoComplete="off"
        value={query}
        onChange={(event) => {
          setQuery(event.target.value);
          setActive(0);
        }}
        onKeyDown={onKeyDown}
        slotProps={{
          htmlInput: {
            role: 'combobox',
            'aria-expanded': entries.length > 0,
            'aria-controls': listId,
            'aria-activedescendant': entries.length > 0 ? optionId(current) : undefined,
            spellCheck: false
          }
        }}
      />

      {entries.length > 0 ? (
        <div id={listId} role="listbox" aria-label="Resultados" className={styles.list}>
          {groups.map(([group, items], position) => (
            <div key={group} role="group" aria-labelledby={`${listId}-grupo-${position}`} className={styles.group}>
              <div id={`${listId}-grupo-${position}`} role="presentation" className={styles.groupLabel}>
                {group}
              </div>
              {items.map(({ entry, index }) => (
                <button
                  key={entry.id}
                  type="button"
                  id={optionId(index)}
                  role="option"
                  tabIndex={-1}
                  aria-selected={index === current}
                  className={cn(styles.option, index === current && styles.active)}
                  onClick={() => run(entry)}
                  onMouseMove={() => setActive(index)}
                >
                  <span className={styles.optionText}>
                    <span>{entry.label}</span>
                    {entry.description ? <span className={styles.description}>{entry.description}</span> : null}
                  </span>
                  <span className={styles.hint}>{entry.hint}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      ) : (
        <p role="status" className={styles.empty}>
          Nada encontrado para &quot;{query.trim()}&quot;. Escreva uma frase com o que precisa para abrir um pedido.
        </p>
      )}
    </div>
  );
}
