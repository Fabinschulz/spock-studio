import { Stage } from '@/prototype/components/stage';
import { demoProject } from '@/prototype/data/demo-project';
import screen from '@/prototype/styles/screen.module.scss';

import { HarnessNotice } from './components/harness-notice';
import { InventorySection } from './components/inventory-section';
import { ProjectContext } from './components/project-context';
import { RunsSection } from './components/runs-section';
import { StackSection } from './components/stack-section';

export function ProjectScreen() {
  return (
    <Stage
      bar={
        <>
          <span className={screen.barTitle}>{demoProject.name}</span>
          <span className={screen.barSub}>Projeto</span>
        </>
      }
      panelTitle="Contexto"
      panel={<ProjectContext />}
    >
      <header className={screen.head}>
        <h1 className={screen.title}>{demoProject.name}</h1>
        <p className={`${screen.lead} ${screen.mono}`}>{demoProject.path}</p>
      </header>

      <HarnessNotice />
      <InventorySection />
      <StackSection />
      <RunsSection />
    </Stage>
  );
}
