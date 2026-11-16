import { EmptyState, Page, PageSection, ResourceView } from '@/shared/components';
import { app } from '@/shared/config/app';
import { useAsyncResource, useEngineStatus } from '@/shared/hooks';
import { useCallback } from 'react';

import styles from './about-page.module.scss';
import { readThirdPartyNotices, type ThirdPartyNotices } from './service/notices-api';

export const PRODUCT_MEANING = 'System of Programming, Organization, Code and Knowledge';

export function AboutPage() {
  const engine = useEngineStatus();
  const read = useCallback(
    async (): Promise<ThirdPartyNotices | null> => (engine.hosted ? readThirdPartyNotices() : null),
    [engine.hosted]
  );
  const notices = useAsyncResource(read);

  return (
    <Page title="Sobre" description={`SPOCK: ${PRODUCT_MEANING}.`}>
      <PageSection title="Aplicativo">
        <p>
          {app.name} v{app.version}. {app.tagline}.
        </p>
      </PageSection>

      <PageSection
        title="Avisos de terceiros"
        description="Este aplicativo inclui software de terceiros. Os textos de licença abaixo são reproduzidos sem alteração."
      >
        <ResourceView
          resource={notices}
          loading="Lendo os avisos…"
          fallbackError="Não foi possível ler os avisos de terceiros desta instalação."
        >
          {(value) =>
            value === null ? (
              <EmptyState>Os avisos de terceiros aparecem no aplicativo instalado.</EmptyState>
            ) : (
              <>
                <p className={styles.summary}>
                  {value.components} componentes de terceiros.
                  {value.complete ? '' : ' Alguns textos de licença ainda estão sendo reunidos.'}
                </p>
                {/* Bloco rolável: precisa de foco para quem navega só pelo teclado. */}
                {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex */}
                <pre className={styles.notices} tabIndex={0} aria-label="Textos de licença de terceiros">
                  {value.text}
                </pre>
              </>
            )
          }
        </ResourceView>
      </PageSection>
    </Page>
  );
}
