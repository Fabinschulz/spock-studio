import { useSearchParams } from 'react-router';

import type { Contract, ForcedState } from '@/prototype/types/journey';

// Parâmetros de URL só do protótipo: ?contrato=proposto, ?t=<ms> (instante fixo) e ?estado=<estado intermediário>.
export function usePrototypeParams() {
  const [search, setSearch] = useSearchParams();

  const contract: Contract = search.get('contrato') === 'proposto' ? 'proposed' : 'current';
  const fixedAt = search.get('t') === null ? null : Number(search.get('t'));
  const forced = search.get('estado') as ForcedState | null;
  const keepSearch = search.toString() ? `?${search.toString()}` : '';

  const setContract = (value: Contract) => {
    const next = new URLSearchParams(search);
    if (value === 'proposed') next.set('contrato', 'proposto');
    else next.delete('contrato');
    setSearch(next, { replace: true });
  };

  return { contract, fixedAt, forced, keepSearch, setContract };
}
