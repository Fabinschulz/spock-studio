import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { InlineNotice, NoProjectState, Page, PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { SecurityReport } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';

import { FindingList } from './components/finding-list';
import { FixPanel } from './components/fix-panel';
import { useSecurityFix } from './hooks/use-security-fix';
import styles from './security-page.module.scss';
import { createSecurityApi, type SecurityApi } from './service/security-api';

export function SecurityPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createSecurityApi(client), [client]);
  if (!project) {
    return (
      <Page title="Segurança">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectSecurity key={project.projectId} api={api} project={project} />;
}

function ProjectSecurity({ api, project }: { api: SecurityApi; project: SelectedProject }) {
  const report = useAsyncResource(useCallback(() => api.scan(project.projectId), [api, project.projectId]));
  const rescan = report.reload ?? (() => undefined);
  const { fix, review, apply, revert, close } = useSecurityFix(api, project.projectId, rescan);
  const busy = fix.status === 'planning' || fix.status === 'applying' || fix.status === 'reverting';

  return (
    <Page
      title="Segurança"
      description={`Credenciais no código, arquivos sensíveis sem proteção e permissões arriscadas das ferramentas de IA em ${project.displayName}. A varredura só lê; corrigir passa por diferença e aprovação, e pode ser desfeito.`}
      actions={
        <Button
          type="button"
          variant="outlined"
          color="inherit"
          disabled={busy || report.status === 'loading'}
          onClick={rescan}
        >
          Varrer de novo
        </Button>
      }
    >
      <FixPanel
        fix={fix}
        onApply={(plan) => void apply(plan)}
        onRevert={(result) => void revert(result)}
        onClose={close}
      />
      <ResourceView resource={report} loading="Varrendo o projeto…" fallbackError="Não foi possível varrer o projeto.">
        {(value) => (
          <Findings report={value} canFix={!busy && fix.status !== 'reviewing'} onReview={() => void review()} />
        )}
      </ResourceView>
    </Page>
  );
}

function Findings({ report, canFix, onReview }: { report: SecurityReport; canFix: boolean; onReview: () => void }) {
  const fixable = report.findings.filter((finding) => finding.fixable);
  const manual = report.findings.filter((finding) => !finding.fixable);
  const high = report.findings.filter((finding) => finding.severity === 'high').length;

  return (
    <>
      <p className={styles.summary} role="status">
        {report.findings.length === 0
          ? `Nada encontrado em ${report.filesScanned} arquivos lidos.`
          : `${report.findings.length} achados, ${high} graves, em ${report.filesScanned} arquivos lidos.`}
        {report.truncated ? ' O projeto passou do limite de arquivos lidos; parte dele ficou de fora.' : ''}
      </p>
      {report.findings.length === 0 ? (
        <InlineNotice tone="success" title="Nenhum problema encontrado">
          O Spock procura os formatos de credencial e as configurações que conhece; isso não substitui uma revisão de
          segurança completa.
        </InlineNotice>
      ) : null}
      {fixable.length > 0 ? (
        <PageSection
          title="O Spock corrige com a sua aprovação"
          action={
            <Button type="button" variant="contained" disabled={!canFix} onClick={onReview}>
              Revisar a correção
            </Button>
          }
        >
          <FindingList findings={fixable} />
        </PageSection>
      ) : null}
      {manual.length > 0 ? (
        <PageSection
          title="Precisa de você"
          description="Mexer em código ou fora das pastas do Spock fica com você. O valor encontrado nunca aparece aqui."
        >
          <FindingList findings={manual} />
        </PageSection>
      ) : null}
    </>
  );
}
