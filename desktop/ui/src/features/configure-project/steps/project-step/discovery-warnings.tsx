import { EvidenceDisclosure, InlineNotice } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

const NOT_A_GIT_REPOSITORY = 'NOT_A_GIT_REPOSITORY';

export function DiscoveryWarnings({ warnings }: { warnings: Discovery['warnings'] }) {
  return warnings.map((warning) => {
    const withoutGit = warning.code === NOT_A_GIT_REPOSITORY;

    return (
      <InlineNotice key={warning.code + warning.message} tone={withoutGit ? 'info' : 'warning'}>
        <span>{warning.message}</span>
        {withoutGit ? (
          <span>
            {' '}
            A reversão continua possível pelo backup que a aplicação guarda antes de alterar qualquer arquivo.
          </span>
        ) : null}
        {warning.paths.length > 0 ? (
          <EvidenceDisclosure summary={`Arquivos (${warning.paths.length})`} items={warning.paths} />
        ) : null}
      </InlineNotice>
    );
  });
}
