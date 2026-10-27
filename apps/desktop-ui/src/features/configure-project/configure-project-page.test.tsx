import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect, type ReactNode } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import applyResult from '@contracts/fixtures/golden/apply-result.json';
import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import revertResult from '@contracts/fixtures/golden/revert-result.json';
import validateResult from '@contracts/fixtures/golden/validate-result.json';

import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { discoverySchema, planSchema, runResultSchema, type RunResult } from '@/shared/schemas';
import { EngineError, type HarnessApi } from '@/shared/services';

import { ConfigureProjectPage } from './configure-project-page';

const discovery = discoverySchema.parse(discoveryFixture);
const plan = planSchema.parse(planFixture);
const project = { projectId: 'p-1', displayName: 'EmpregaNetAPI', displayPath: '~\\EmpregaNetAPI' };

const conflict = plan.changes.find((c) => c.action === 'conflict')!;
const command = plan.commands[0]!;
const creates = plan.changes.filter((c) => c.action === 'create' || c.action === 'update');

function snapshotOf(result: RunResult) {
  return {
    runId: result.runId,
    projectId: result.projectId,
    kind: result.kind,
    state: result.outcome,
    lastSeq: 0,
    events: [],
    result
  };
}

function fakeApi(overrides: Partial<HarnessApi> = {}): HarnessApi {
  const apply = runResultSchema.parse(applyResult);
  return {
    discover: vi.fn().mockResolvedValue(discovery),
    plan: vi.fn().mockResolvedValue(plan),
    apply: vi.fn().mockResolvedValue({ runId: apply.runId }),
    validate: vi.fn().mockResolvedValue({ runId: 'run-validate' }),
    revert: vi.fn().mockResolvedValue({ runId: 'run-revert' }),
    snapshot: vi.fn().mockResolvedValue(snapshotOf(apply)),
    cancel: vi.fn().mockResolvedValue({ cancelled: true }),
    runs: vi.fn().mockResolvedValue([]),
    onEvent: vi.fn().mockResolvedValue(() => undefined),
    ...overrides
  };
}

/** A jornada só abre com uma pasta escolhida; aqui ela é escolhida antes de montar a página. */
function WithProject({ children }: { children: ReactNode }) {
  const { selected, pick } = useProjectSelectionContext();
  useEffect(() => {
    void pick();
  }, [pick]);
  return selected ? children : null;
}

function renderJourney(api: HarnessApi) {
  return render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(project) }}>
        <MemoryRouter initialEntries={['/configurar']}>
          <Routes>
            <Route
              path="/configurar"
              element={
                <WithProject>
                  <ConfigureProjectPage api={api} pollMs={5} />
                </WithProject>
              }
            />
            <Route path="/" element={<p>TELA INICIAL</p>} />
          </Routes>
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
}

const click = (name: RegExp | string) => userEvent.click(screen.getByRole('button', { name }));

async function reachReview(api: HarnessApi) {
  renderJourney(api);
  await screen.findByText('Stack detectada');
  await click('Continuar');
  await click('Gerar plano');
  await screen.findByText('Composição do Harness');
  await click('Revisar alterações');
  await screen.findByText('Revisão das alterações');
}

describe('jornada Configurar projeto', () => {
  describe('A · projeto e descoberta', () => {
    it('mostra o que o motor detectou, com evidência, antes de qualquer decisão', async () => {
      renderJourney(fakeApi());

      expect(await screen.findByText('Projeto EmpregaNetAPI')).toBeInTheDocument();
      expect(screen.getByText(/A análise é somente de leitura/)).toBeInTheDocument();
      const stacks = within(await screen.findByRole('list', { name: 'Stacks detectadas' }));
      expect(stacks.getByText(/\.NET net10\.0/)).toBeInTheDocument();
      expect(stacks.getByText('confiança alta')).toBeInTheDocument();
      expect(screen.getByText('Não instalado')).toBeInTheDocument();
      expect(screen.getByText(/Git: 2\.56\.0|Git: versão 2\.56\.0/)).toBeInTheDocument();
    });

    it('mostra o aviso de que a pasta não é um repositório git', async () => {
      renderJourney(fakeApi());

      expect(await screen.findByText(/A pasta nã/)).toBeInTheDocument();
    });

    it('permite tentar de novo quando a análise falha, sem mostrar resultado parcial', async () => {
      const discover = vi
        .fn()
        .mockRejectedValueOnce(new EngineError('INTERNAL', 'O motor não respondeu.'))
        .mockResolvedValue(discovery);
      renderJourney(fakeApi({ discover }));

      expect(await screen.findByText('Não foi possível analisar o projeto')).toBeInTheDocument();
      expect(screen.getByText('O motor não respondeu.')).toBeInTheDocument();
      expect(screen.queryByText('Stack detectada')).not.toBeInTheDocument();

      await click('Tentar de novo');
      expect(await screen.findByText('Stack detectada')).toBeInTheDocument();
      expect(discover).toHaveBeenCalledTimes(2);
    });
  });

  describe('B · método e ambiente', () => {
    it('apresenta o TLC como único método e sugere o único ambiente suportado detectado', async () => {
      renderJourney(fakeApi());
      await screen.findByText('Stack detectada');
      await click('Continuar');

      expect(await screen.findByText('Método e ambiente de IA')).toBeInTheDocument();
      expect(screen.getByText(/TLC \(Tech Leads Club\)/)).toBeInTheDocument();
      expect(screen.getByText(/somente se você aprovar o comando/)).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: /Claude Code/ })).toBeChecked();
      expect(screen.getByRole('button', { name: 'Gerar plano' })).toBeEnabled();
    });

    it('sem ambiente detectado, exige a escolha da pessoa antes de gerar o plano', async () => {
      const api = fakeApi({ discover: vi.fn().mockResolvedValue({ ...discovery, aiEnvironments: [] }) });
      renderJourney(api);
      await screen.findByText('Stack detectada');
      await click('Continuar');

      expect(await screen.findByRole('button', { name: 'Gerar plano' })).toBeDisabled();
      await userEvent.click(screen.getByRole('radio', { name: /Claude Code/ }));
      expect(screen.getByRole('button', { name: 'Gerar plano' })).toBeEnabled();
    });

    it('avisa que Cursor e Codex não têm configuração gerada nesta fase', async () => {
      renderJourney(fakeApi());
      await screen.findByText('Stack detectada');
      await click('Continuar');
      await screen.findByText('Método e ambiente de IA');

      await userEvent.click(screen.getByRole('radio', { name: /Cursor/ }));

      expect(screen.getByText('Ambiente sem suporte nesta fase')).toBeInTheDocument();
    });

    it('só oferece reinstalar o TLC quando ele está alterado, e o envia como permissão explícita', async () => {
      const drifted = { ...discovery, tlc: { ...discovery.tlc, state: 'drifted' as const, version: '3.3.0' } };
      const api = fakeApi({ discover: vi.fn().mockResolvedValue(drifted) });
      renderJourney(api);
      await screen.findByText('Stack detectada');
      await click('Continuar');

      const reinstall = await screen.findByRole('checkbox', { name: /Permitir reinstalar o TLC/ });
      expect(reinstall).not.toBeChecked();
      await userEvent.click(reinstall);
      await click('Gerar plano');

      await waitFor(() =>
        expect(api.plan).toHaveBeenCalledWith({
          projectId: 'p-1',
          aiEnvironment: 'claude-code',
          allowTlcForceReinstall: true,
          updateTlc: false
        })
      );
    });

    it('com o TLC instalado, oferece buscar a versão mais recente e envia o pedido ao motor', async () => {
      const installed = {
        ...discovery,
        tlc: { ...discovery.tlc, state: 'installed' as const, version: '3.3.0', versionTested: true }
      };
      const api = fakeApi({ discover: vi.fn().mockResolvedValue(installed) });
      renderJourney(api);
      await screen.findByText('Stack detectada');
      await click('Continuar');

      const update = await screen.findByRole('checkbox', { name: /Buscar a versão mais recente do TLC/ });
      expect(update).not.toBeChecked();
      expect(screen.getByText(/Usa a rede e só executa se você aprovar/)).toBeInTheDocument();
      await userEvent.click(update);
      await click('Gerar plano');

      await waitFor(() =>
        expect(api.plan).toHaveBeenCalledWith({
          projectId: 'p-1',
          aiEnvironment: 'claude-code',
          allowTlcForceReinstall: false,
          updateTlc: true
        })
      );
    });

    it('sem o TLC instalado não oferece atualizar: a instalação já traz a versão mais recente', async () => {
      renderJourney(fakeApi());
      await screen.findByText('Stack detectada');
      await click('Continuar');
      await screen.findByText('Método e ambiente de IA');

      expect(screen.queryByRole('checkbox', { name: /Buscar a versão mais recente do TLC/ })).not.toBeInTheDocument();
    });

    it('não oferece reinstalar quando o TLC não está alterado', async () => {
      renderJourney(fakeApi());
      await screen.findByText('Stack detectada');
      await click('Continuar');
      await screen.findByText('Método e ambiente de IA');

      expect(screen.queryByRole('checkbox', { name: /Permitir reinstalar o TLC/ })).not.toBeInTheDocument();
    });

    it('mantém a pessoa nesta etapa, com a mensagem do motor, quando o plano falha', async () => {
      const api = fakeApi({ plan: vi.fn().mockRejectedValue(new EngineError('PLAN_FAILED', 'Plano inválido.')) });
      renderJourney(api);
      await screen.findByText('Stack detectada');
      await click('Continuar');
      await click('Gerar plano');

      expect(await screen.findByText('Não foi possível gerar o plano')).toBeInTheDocument();
      expect(screen.getByText('Plano inválido.')).toBeInTheDocument();
      expect(screen.queryByText('Composição do Harness')).not.toBeInTheDocument();
    });
  });

  describe('C · plano do Harness Engineer', () => {
    it('mostra cada componente com decisão, origem e justificativa, e diz quando a proposta é só das regras-base', async () => {
      renderJourney(fakeApi());
      await screen.findByText('Stack detectada');
      await click('Continuar');
      await click('Gerar plano');

      const table = within(await screen.findByRole('region', { name: 'Decisões por componente' }));
      expect(table.getByText('mariliz.agent.harness-engineer')).toBeInTheDocument();
      expect(table.getByText('mariliz.rule.stack-javascript')).toBeInTheDocument();
      expect(table.getAllByText('Dispensar').length).toBeGreaterThan(0);
      expect(table.getAllByText(/Origem: Regras-base/).length).toBeGreaterThan(0);
      expect(screen.getByText('Plano pelas regras-base')).toBeInTheDocument();
    });
  });

  describe('D · revisão e aprovações', () => {
    it('marca por padrão só criar e atualizar; comandos desmarcados e conflito em "manter o meu arquivo"', async () => {
      await reachReview(fakeApi());

      const commandBox = screen.getByRole('checkbox', { name: /Aprovar a execução deste comando/ });
      expect(commandBox).not.toBeChecked();
      expect(screen.getByText(/Desmarcado por padrão/)).toBeInTheDocument();

      expect(screen.getByRole('radio', { name: /Manter o meu arquivo/ })).toBeChecked();
      expect(screen.getByRole('radio', { name: /Substituir pelo proposto/ })).not.toBeChecked();

      const created = screen.getByRole('checkbox', { name: 'Aprovar criar mariliz.yaml' });
      expect(created).toBeChecked();
      expect(
        screen.getByRole('button', { name: `Aplicar configuração (${creates.length} aprovados)` })
      ).toBeInTheDocument();
    });

    it('mostra o comando exato, seus efeitos e as pastas afetadas', async () => {
      await reachReview(fakeApi());

      expect(screen.getByText('Instalar o TLC pelo instalador oficial')).toBeInTheDocument();
      expect(
        screen.getByText('npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven -a claude-code')
      ).toBeInTheDocument();
      expect(screen.getByText('Acessa a rede')).toBeInTheDocument();
      expect(screen.getByText('Executa um programa')).toBeInTheDocument();
    });

    it('revela o diff só quando pedido e nunca para arquivos sem alteração', async () => {
      await reachReview(fakeApi());

      expect(screen.queryByRole('group', { name: /Diferenças em/ })).not.toBeInTheDocument();
      const buttons = screen.getAllByRole('button', { name: 'Ver diferenças' });
      await userEvent.click(buttons[0]!);

      expect(screen.getByLabelText(/Diferenças em /)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Ocultar diferenças' })).toHaveAttribute('aria-expanded', 'true');
    });

    it('aplica enviando o plano, as aprovações e "manter" para o conflito', async () => {
      const api = fakeApi();
      await reachReview(api);

      await click(/Aplicar configuração/);

      await waitFor(() => expect(api.apply).toHaveBeenCalledTimes(1));
      const sent = vi.mocked(api.apply).mock.calls[0]![0];
      expect(sent.projectId).toBe('p-1');
      expect(sent.planId).toBe(plan.planId);
      expect(sent.approvedChangeIds.sort()).toEqual(creates.map((c) => c.id).sort());
      expect(sent.approvedChangeIds).not.toContain(conflict.id);
      expect(sent.approvedCommandIds).toEqual([]);
      expect(sent.resolutions).toEqual({ [conflict.id]: 'keep' });
    });

    it('substituir um conflito conta como aprovação e é enviado como "overwrite"', async () => {
      const api = fakeApi();
      await reachReview(api);

      await userEvent.click(screen.getByRole('radio', { name: /Substituir pelo proposto/ }));
      expect(
        screen.getByRole('button', { name: `Aplicar configuração (${creates.length + 1} aprovados)` })
      ).toBeInTheDocument();
      await click(/Aplicar configuração/);

      await waitFor(() => expect(api.apply).toHaveBeenCalled());
      const sent = vi.mocked(api.apply).mock.calls[0]![0];
      expect(sent.approvedChangeIds).toContain(conflict.id);
      expect(sent.resolutions[conflict.id]).toBe('overwrite');
    });

    it('envia o comando somente depois de a pessoa aprová-lo', async () => {
      const api = fakeApi();
      await reachReview(api);

      await userEvent.click(screen.getByRole('checkbox', { name: /Aprovar a execução deste comando/ }));
      await click(/Aplicar configuração/);

      await waitFor(() => expect(api.apply).toHaveBeenCalled());
      expect(vi.mocked(api.apply).mock.calls[0]![0].approvedCommandIds).toEqual([command.id]);
    });

    it('não permite aplicar sem nenhuma aprovação e explica por quê', async () => {
      const api = fakeApi();
      await reachReview(api);

      for (const box of screen.getAllByRole('checkbox', { name: /^Aprovar (criar|atualizar)/ })) {
        await userEvent.click(box);
      }

      const apply = screen.getByRole('button', { name: 'Nada aprovado para aplicar' });
      expect(apply).toBeDisabled();
      expect(api.apply).not.toHaveBeenCalled();
    });

    it('"Cancelar sem aplicar" volta ao início sem chamar o motor para escrever', async () => {
      const api = fakeApi();
      await reachReview(api);

      await click('Cancelar sem aplicar');

      expect(await screen.findByText('TELA INICIAL')).toBeInTheDocument();
      expect(api.apply).not.toHaveBeenCalled();
    });
  });

  describe('E e F · aplicação e resultado', () => {
    it('mostra o resultado com Evals e distingue "não executado" de "aprovado"', async () => {
      await reachReview(fakeApi());
      await click(/Aplicar configuração/);

      expect(await screen.findByRole('heading', { name: 'Resultado' })).toBeInTheDocument();
      expect(screen.getByText('Concluído')).toBeInTheDocument();
      expect(screen.getByText(/Todas as verificações obrigatórias foram aprovadas/)).toBeInTheDocument();

      const summary = within(screen.getByLabelText('Resumo dos Evals'));
      expect(summary.getByText('12 aprovado')).toBeInTheDocument();
      expect(summary.getByText('0 reprovado')).toBeInTheDocument();
      expect(summary.getByText('0 bloqueado')).toBeInTheDocument();
      expect(summary.getByText('2 não executado')).toBeInTheDocument();

      const evals = within(screen.getByRole('region', { name: 'Resultados dos Evals' }));
      expect(evals.getByText(/EV-JUDGE-001/)).toBeInTheDocument();
      expect(evals.getAllByText('Não executado').length).toBe(2);
    });

    it('lista os arquivos criados e mantém o relato do que foi feito', async () => {
      await reachReview(fakeApi());
      await click(/Aplicar configuração/);

      await screen.findByRole('heading', { name: 'Resultado' });
      expect(screen.getByText('11 criados')).toBeInTheDocument();
      expect(screen.getByText('0 não aplicados')).toBeInTheDocument();
    });

    it('mostra o progresso vindo dos eventos do motor enquanto a execução não termina', async () => {
      const done = runResultSchema.parse(applyResult);
      let release: () => void = () => undefined;
      const gate = new Promise<void>((resolve) => (release = resolve));
      const handlers: ((e: never) => void)[] = [];
      const api = fakeApi({
        onEvent: vi.fn().mockImplementation(async (h) => {
          handlers.push(h);
          return () => undefined;
        }),
        snapshot: vi.fn().mockImplementation(async () => {
          await gate;
          return snapshotOf(done);
        })
      });
      await reachReview(api);
      await click(/Aplicar configuração/);

      await screen.findByText('Aplicação em andamento');
      await waitFor(() => expect(handlers.length).toBeGreaterThan(0));
      handlers[0]!({
        runId: done.runId,
        seq: 1,
        type: 'step.started',
        data: { id: 'backup', title: 'Guardando cópias de segurança' }
      } as never);

      expect(await screen.findByText('Guardando cópias de segurança')).toBeInTheDocument();
      expect(screen.getByText('em andamento')).toBeInTheDocument();

      release();
      expect(await screen.findByRole('heading', { name: 'Resultado' })).toBeInTheDocument();
    });

    it('cancela uma execução em andamento pelo motor, que desfaz o que já foi feito', async () => {
      const cancelled = {
        ...runResultSchema.parse(applyResult),
        outcome: 'cancelled' as const,
        changesApplied: false,
        rolledBack: true,
        rollbackAvailable: false,
        validation: undefined
      };
      let wasCancelled = false;
      const api = fakeApi({
        snapshot: vi
          .fn()
          .mockImplementation(async () =>
            wasCancelled ? snapshotOf(cancelled) : { ...snapshotOf(cancelled), state: 'running', result: null }
          ),
        cancel: vi.fn().mockImplementation(async () => {
          wasCancelled = true;
          return { cancelled: true };
        })
      });
      await reachReview(api);
      await click(/Aplicar configuração/);

      const cancel = await screen.findByRole('button', { name: 'Cancelar e desfazer' });
      await waitFor(() => expect(cancel).toBeEnabled());
      await userEvent.click(cancel);

      expect(await screen.findByText('Cancelado')).toBeInTheDocument();
      expect(api.cancel).toHaveBeenCalledWith(cancelled.runId);
      expect(screen.getByText(/alterações já feitas foram desfeitas/)).toBeInTheDocument();
    });

    it('uma falha do instalador aparece como falha, com o código, e não como sucesso', async () => {
      const failure = runResultSchema.parse(applyFailure);
      const api = fakeApi({ snapshot: vi.fn().mockResolvedValue(snapshotOf(failure)) });
      await reachReview(api);
      await click(/Aplicar configuração/);

      expect(await screen.findByText('Falhou')).toBeInTheDocument();
      expect(screen.getByText(/COMMAND_FAILED: O instalador terminou com código 1/)).toBeInTheDocument();
      expect(screen.queryByText('Concluído')).not.toBeInTheDocument();
      expect(screen.queryByText(/Todas as verificações obrigatórias foram aprovadas/)).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Reverter alterações' })).not.toBeInTheDocument();
    });

    it('conclusão com limitações é apresentada como limitada, nunca como concluída', async () => {
      const limited = {
        ...runResultSchema.parse(applyResult),
        outcome: 'completed_with_limitations' as const,
        limitations: [{ code: 'TLC_PYTHON_MISSING', message: 'Python ausente: os gates do TLC não puderam rodar.' }]
      };
      const api = fakeApi({ snapshot: vi.fn().mockResolvedValue(snapshotOf(limited)) });
      await reachReview(api);
      await click(/Aplicar configuração/);

      expect(await screen.findByText('Concluído com limitações')).toBeInTheDocument();
      expect(screen.getByText(/Python ausente: os gates do TLC/)).toBeInTheDocument();
      expect(screen.queryByText('Todas as verificações obrigatórias foram aprovadas com evidência.')).toBeNull();
    });

    it('uma falha ao acompanhar a execução é mostrada e permite voltar à revisão', async () => {
      const api = fakeApi({ apply: vi.fn().mockRejectedValue(new EngineError('STALE_PLAN', 'O projeto mudou.')) });
      await reachReview(api);
      await click(/Aplicar configuração/);

      expect(await screen.findByText('Não foi possível acompanhar a execução')).toBeInTheDocument();
      expect(screen.getByText('O projeto mudou.')).toBeInTheDocument();
      await click('Voltar à revisão');
      expect(await screen.findByText('Revisão das alterações')).toBeInTheDocument();
    });

    it('reverter exige confirmação, chama o motor e mostra o resultado conferido', async () => {
      const apply = runResultSchema.parse(applyResult);
      const revert = runResultSchema.parse(revertResult);
      const api = fakeApi({
        snapshot: vi
          .fn()
          .mockImplementation(async (runId: string) => snapshotOf(runId === 'run-revert' ? revert : apply))
      });
      await reachReview(api);
      await click(/Aplicar configuração/);
      await screen.findByRole('heading', { name: 'Resultado' });

      await click('Reverter alterações');
      expect(await screen.findByText('Reverter as alterações?')).toBeInTheDocument();
      expect(api.revert).not.toHaveBeenCalled();

      await click('Reverter agora');

      await waitFor(() => expect(api.revert).toHaveBeenCalledWith('p-1', apply.runId));
      expect(
        await screen.findByText(/Conferido: os arquivos restaurados voltaram ao estado anterior/)
      ).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Reverter alterações' })).not.toBeInTheDocument();
    });

    it('desistir na confirmação não reverte nada', async () => {
      const api = fakeApi();
      await reachReview(api);
      await click(/Aplicar configuração/);
      await screen.findByRole('heading', { name: 'Resultado' });

      await click('Reverter alterações');
      await click('Não reverter');

      expect(api.revert).not.toHaveBeenCalled();
    });

    it('validar de novo substitui os Evals mostrados pelos da nova validação', async () => {
      const apply = runResultSchema.parse(applyResult);
      const validate = runResultSchema.parse(validateResult);
      const api = fakeApi({
        snapshot: vi
          .fn()
          .mockImplementation(async (runId: string) => snapshotOf(runId === 'run-validate' ? validate : apply))
      });
      await reachReview(api);
      await click(/Aplicar configuração/);
      await screen.findByRole('heading', { name: 'Resultado' });
      expect(within(screen.getByLabelText('Resumo dos Evals')).getByText('2 não executado')).toBeInTheDocument();

      await click('Validar de novo');

      await waitFor(() => expect(api.validate).toHaveBeenCalledWith('p-1'));
      await waitFor(() =>
        expect(within(screen.getByLabelText('Resumo dos Evals')).getByText('1 não executado')).toBeInTheDocument()
      );
    });

    it('"Concluir" volta à tela inicial', async () => {
      await reachReview(fakeApi());
      await click(/Aplicar configuração/);
      await screen.findByRole('heading', { name: 'Resultado' });

      await click('Concluir');

      expect(await screen.findByText('TELA INICIAL')).toBeInTheDocument();
    });
  });

  describe('sem projeto', () => {
    it('pede para voltar ao início e escolher a pasta', () => {
      render(
        <AppProviders>
          <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
            <MemoryRouter initialEntries={['/configurar']}>
              <Routes>
                <Route path="/configurar" element={<ConfigureProjectPage api={fakeApi()} />} />
                <Route path="/" element={<p>TELA INICIAL</p>} />
              </Routes>
            </MemoryRouter>
          </ProjectSelectionProvider>
        </AppProviders>
      );

      expect(screen.getByText('Selecione um projeto')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Voltar ao início' })).toBeInTheDocument();
    });
  });
});
