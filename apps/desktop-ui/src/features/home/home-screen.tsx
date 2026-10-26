import { ActionCard, ProjectPicker } from '@mariliz/ui';
import { Box, Typography } from '@mui/material';
import { Alert, BookUploadIcon, ListViewRectangleIcon, UpdateIcon, UserConnectionsIcon } from '@synthra.io/ui-kit';
import type { ReactNode } from 'react';

import type { ProjectPickerPort } from '@/features/project-selection/project-picker-port';
import { useProjectSelection } from '@/features/project-selection/use-project-selection';

import styles from './home-screen.module.scss';
import { journeys, type JourneyId } from './journeys';

// A Synthra não tem ícones de pasta, código ou documento (docs/experience-blueprint.md, seção 11.2): ícones provisórios.
const icons: Record<JourneyId, ReactNode> = {
  'configure-project': <ListViewRectangleIcon />,
  'import-requirements': <BookUploadIcon />,
  'connect-tools': <UserConnectionsIcon />,
  'modernize-system': <UpdateIcon />
};

export type HomeScreenProps = {
  picker: ProjectPickerPort;
  /** Definido quando o host desktop não está presente (ex.: abrindo no navegador). */
  pickerUnavailableReason?: string;
  onStartJourney?: (id: JourneyId) => void;
};

export function HomeScreen({ picker, pickerUnavailableReason, onStartJourney }: HomeScreenProps) {
  const { selected, busy, error, pick } = useProjectSelection(picker);

  return (
    <main className={styles.screen}>
      <header className={styles.brand}>
        <svg className={styles.mark} width="36" height="36" viewBox="0 0 36 36" aria-hidden="true" focusable="false">
          <rect width="36" height="36" rx="9" fill="currentColor" />
          <path
            d="M9 26V10l9 10 9-10v16"
            stroke="var(--mark-ink)"
            strokeWidth="2.6"
            strokeLinecap="round"
            strokeLinejoin="round"
            fill="none"
          />
        </svg>
        <Box>
          <Typography variant="h6" component="p" sx={{ fontWeight: 600, lineHeight: 1.2 }}>
            Mariliz AI Engineering
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            Engenharia de software assistida por IA
          </Typography>
        </Box>
      </header>

      <Box>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
          Como podemos ajudar?
        </Typography>
        <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: '62ch' }}>
          Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.
        </Typography>
      </Box>

      <ul className={styles.grid} aria-label="Atividades">
        {journeys.map((journey) => (
          <li key={journey.id}>
            <ActionCard
              title={journey.title}
              description={journey.description}
              icon={icons[journey.id]}
              unavailableReason={journey.unavailableReason}
              onClick={() => onStartJourney?.(journey.id)}
            />
          </li>
        ))}
      </ul>

      {error ? <Alert severity="error" description={error} /> : null}

      <ProjectPicker selected={selected} busy={busy} onPick={pick} unavailableReason={pickerUnavailableReason} />
    </main>
  );
}
