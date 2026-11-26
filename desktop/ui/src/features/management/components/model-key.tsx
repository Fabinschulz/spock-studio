import { InlineNotice } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import type { AskError } from '../hooks/use-ask';

function KeySettingsLink() {
  return (
    <>
      Configure a chave em <Link to={pathTo('connectionsModels')}>Conexões › Modelos de IA</Link>.
    </>
  );
}

/** `children` diz o que o modelo faria nesta tela; o caminho para a chave é sempre o mesmo. */
export function ModelKeyNotice({ children }: { children: ReactNode }) {
  return (
    <InlineNotice tone="warning" title="Nenhuma chave de modelo configurada">
      {children} <KeySettingsLink />
    </InlineNotice>
  );
}

/** Erro de uma ação (perguntar): aviso em linha, não o estado de erro de carregamento, que oferece "Tentar de novo". */
export function AskErrorNotice({ error }: { error: AskError }) {
  return (
    <InlineNotice tone="danger">
      {error.message}
      {error.needsKey ? (
        <>
          {' '}
          <KeySettingsLink />
        </>
      ) : null}
    </InlineNotice>
  );
}
