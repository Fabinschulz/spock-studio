import { Button } from '@synthra.io/ui-kit';
import { useNavigate } from 'react-router';

import { ProjectComposer } from '@/prototype/components/project-composer';
import { Stage } from '@/prototype/components/stage';
import screen from '@/prototype/styles/screen.module.scss';

import { ActivitiesSection } from './components/activities-section';
import { RecentsSection } from './components/recents-section';

export function HomeScreen() {
  const navigate = useNavigate();

  return (
    <Stage bar={<span className={screen.barTitle}>Início</span>}>
      <header className={screen.head}>
        <h1 className={screen.title}>Por onde começamos?</h1>
        <p className={screen.lead}>
          Escolha uma atividade. O Mariliz analisa o projeto, propõe a configuração de engenharia e só altera o que você
          aprovar.
        </p>
      </header>

      <div className={screen.stack}>
        <ProjectComposer>
          <Button variant="text" color="inherit" onClick={() => undefined}>
            Trocar
          </Button>
          <Button variant="contained" onClick={() => navigate('/configurar')}>
            Analisar projeto
          </Button>
        </ProjectComposer>
        <p className={screen.note}>A análise inicial é somente de leitura e não altera seus arquivos.</p>
      </div>

      <ActivitiesSection />
      <RecentsSection />
    </Stage>
  );
}
