import { useProjectSelectionContext } from '@/features/project-selection';
import { cn } from '@/shared/utils';
import { Modal, TextField } from '@mariliz/ui-kit';
import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router';

import { buildCommands, type Command } from './commands';
import styles from './CommandPalette.module.scss';
import { searchCommands } from './search-commands';

export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Modal open={open} onClose={onClose} title="Buscar" closeLabel="Fechar" sizeModal="medium">
      <PaletteBody onClose={onClose} />
    </Modal>
  );
}

function PaletteBody({ onClose }: { onClose: () => void }) {
  const listId = useId();
  const navigate = useNavigate();
  const { recents, openRecent } = useProjectSelectionContext();
  const commands = useMemo(() => buildCommands(recents), [recents]);
  const field = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);

  useEffect(() => {
    field.current?.focus();
  }, []);

  const results = useMemo(() => searchCommands(commands, query), [commands, query]);
  const current = Math.min(active, results.length - 1);
  const optionId = (index: number) => `${listId}-${index}`;

  const run = ({ action }: Command) => {
    onClose();
    if (action.kind === 'go') navigate(action.to);
    else void openRecent(action.recentId);
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (results.length === 0) return;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive((current + step + results.length) % results.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      run(results[current]!);
    }
  };

  return (
    <div className={styles.palette}>
      <TextField
        label="Tela ou projeto"
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
            'aria-expanded': results.length > 0,
            'aria-controls': listId,
            'aria-activedescendant': results.length > 0 ? optionId(current) : undefined,
            spellCheck: false
          }
        }}
      />

      {results.length > 0 ? (
        <ul id={listId} role="listbox" aria-label="Resultados" className={styles.list}>
          {results.map((command, index) => (
            <li key={command.id} role="presentation">
              <button
                type="button"
                id={optionId(index)}
                role="option"
                tabIndex={-1}
                aria-selected={index === current}
                className={cn(styles.option, index === current && styles.active)}
                onClick={() => run(command)}
                onMouseMove={() => setActive(index)}
              >
                <span>{command.label}</span>
                <span className={styles.hint}>{command.hint}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p role="status" className={styles.empty}>
          Nada encontrado para &quot;{query.trim()}&quot;.
        </p>
      )}
    </div>
  );
}
