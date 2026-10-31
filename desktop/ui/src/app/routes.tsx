import { paths } from '@/shared/routing';
import type { RouteObject } from 'react-router';
import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Configure from './routes/configure';
import Execution from './routes/execution';
import Home from './routes/home';
import Settings from './routes/settings';

export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [
      { index: true, element: <Home /> },
      { path: 'configurar', element: <Configure /> },
      { path: 'configuracoes', element: <Settings /> },
      { path: 'execucao/:runId', element: <Execution /> }
    ]
  }
];
