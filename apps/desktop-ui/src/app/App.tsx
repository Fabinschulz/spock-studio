import { CssBaseline, useMediaQuery } from '@mui/material';
import { Provider } from '@synthra.io/ui-kit';

import { themes } from '@/core/theme/theme';
import { HomeScreen } from '@/features/home/home-screen';
import { isTauriRuntime, tauriProjectPicker } from '@/features/project-selection/tauri-project-picker';

const desktop = isTauriRuntime();

export function App() {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)');
  const mode = prefersDark ? 'dark' : 'light';

  return (
    <Provider theme={themes[mode]} cssBaseline={false}>
      <CssBaseline />
      <div data-color-mode={mode} style={{ minHeight: '100%' }}>
        <HomeScreen
          picker={tauriProjectPicker}
          pickerUnavailableReason={desktop ? undefined : 'Disponível apenas no aplicativo desktop.'}
        />
      </div>
    </Provider>
  );
}
