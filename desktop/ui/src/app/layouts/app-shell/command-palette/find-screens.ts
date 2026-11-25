import { buildCommands } from './commands';
import { suggestCommands } from './search-commands';

const SCREENS = buildCommands([]).filter((command) => command.action.kind === 'go');

/** As telas que têm relação com uma frase, para a entrada de pedidos oferecer o caminho direto. */
export function findScreens(sentence: string) {
  return suggestCommands(SCREENS, sentence).flatMap(({ id, label, hint, action }) =>
    action.kind === 'go' ? [{ id, label, hint, to: action.to }] : []
  );
}
