import { Button } from '@synthra.io/ui-kit';
import { useNavigate } from 'react-router';

import { demoProject, demoRuns, discovery } from '../data/fixtures';
import type { Actor } from '../domain/operation-model';
import { ParticipantsSection, SummarySection } from '../shell/context-sections';
import { Stage } from '../shell/stage';
import { Glyph } from '../ui/glyph';
import { Section, StatusGlyph, Tag } from '../ui/primitives';

import styles from './screen.module.scss';

const inventory: [string, keyof typeof discovery.inventory][] = [
  ['Agentes', 'agents'],
  ['Skills', 'skills'],
  ['Regras', 'rules'],
  ['Hooks', 'hooks'],
  ['Servidores MCP', 'mcpServers'],
  ['Evals', 'evals']
];

const idleEngine: Actor = {
  id: 'engine',
  name: 'Motor Mariliz',
  state: 'not_started',
  activity: null,
  source: 'engine'
};

export function ProjectScreen() {
  const navigate = useNavigate();
  const stack = discovery.stacks[0];

  return (
    <Stage
      bar={
        <>
          <span className={styles.barTitle}>{demoProject.name}</span>
          <span className={styles.barSub}>Projeto</span>
        </>
      }
      panelTitle="Contexto"
      panel={
        <>
          <SummarySection
            facts={[
              ['Pasta', demoProject.path],
              ['Stack', stack ? `${stack.name} ${stack.version ?? ''}` : '—'],
              ['Git', discovery.git.isRepository ? 'Repositório' : 'Não é repositório'],
              ['Ambiente de IA', 'Claude Code']
            ]}
          />
          <ParticipantsSection
            participants={[
              {
                ...idleEngine,
                note: 'Executa localmente. Os agentes só aparecem aqui quando houver eventos reais deles.'
              }
            ]}
          />
        </>
      }
    >
      <header className={styles.head}>
        <h1 className={styles.title}>{demoProject.name}</h1>
        <p className={`${styles.lead} ${styles.mono}`}>{demoProject.path}</p>
      </header>

      <div className={styles.notice}>
        <Glyph name="spark" size={16} />
        <div>
          <p>
            <strong>O Harness ainda não está configurado neste projeto.</strong>
          </p>
          <p>A estrutura de especificação não está instalada e não há configuração declarada (mariliz.yaml).</p>
        </div>
        <Button variant="contained" onClick={() => navigate('/configurar')}>
          Configurar projeto
        </Button>
      </div>

      <Section title="O que já existe" id="inventario" aside="encontrado na análise">
        <ul role="list" className={styles.rows}>
          {inventory.map(([label, key]) => {
            const items = discovery.inventory[key];
            return (
              <li key={key} className={styles.row}>
                <span className={styles.rowIcon}>
                  <StatusGlyph
                    state={items.length > 0 ? 'done' : 'skipped'}
                    label={items.length > 0 ? 'encontrado' : 'nenhum'}
                  />
                </span>
                <span>
                  <span className={styles.rowTitle}>{label}</span>
                  {items.length > 0 ? (
                    <span className={`${styles.rowMeta} ${styles.mono}`}> {items.map((i) => i.name).join(', ')}</span>
                  ) : null}
                </span>
                <span className={styles.rowEnd}>
                  {items.length > 0 ? `${items.length}` : 'nenhum'}
                  {items.some((i) => !i.managed) ? <Tag>não gerenciado</Tag> : null}
                </span>
              </li>
            );
          })}
        </ul>
      </Section>

      <Section title="Stack e comandos" id="stack">
        <ul role="list" className={styles.rows}>
          {stack ? (
            <li className={styles.row}>
              <span className={styles.rowIcon}>
                <Glyph name="cpu" size={18} />
              </span>
              <span>
                <span className={styles.rowTitle}>{stack.name}</span>
                <span className={styles.rowMeta}>
                  {' '}
                  {stack.version} · em {stack.root}
                </span>
              </span>
              <Tag tone="success">confiança alta</Tag>
            </li>
          ) : null}
          {discovery.tooling.map((tool) => (
            <li key={tool.kind} className={styles.row}>
              <span className={styles.rowIcon}>
                <Glyph name="clock" size={18} />
              </span>
              <span>
                <span className={styles.rowTitle}>{tool.kind === 'build' ? 'Build' : 'Teste'}</span>{' '}
                <span className={styles.mono}>{tool.command}</span>
              </span>
              <span />
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Execuções" id="execucoes" aside="exemplo">
        <ul role="list" className={styles.rows}>
          {demoRuns.map((run) => (
            <li key={run.id} className={styles.row}>
              <span className={styles.rowIcon}>
                <StatusGlyph state={run.outcome === 'Concluído' ? 'done' : 'warning'} label={run.outcome} />
              </span>
              <span>
                <span className={styles.rowTitle}>{run.kind}</span>
                <span className={styles.rowMeta}> · {run.outcome}</span>
              </span>
              <span className={styles.rowEnd}>{run.when}</span>
            </li>
          ))}
        </ul>
      </Section>
    </Stage>
  );
}
