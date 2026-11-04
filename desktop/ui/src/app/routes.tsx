import { RequiresHost } from '@/shared/components';
import { executionPattern, paths } from '@/shared/routing';
import type { RouteObject } from 'react-router';
import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Configure from './routes/configure';
import EngineeringRequest from './routes/engineering-request';
import Execution from './routes/execution';
import Home from './routes/home';
import Settings from './routes/settings';

// Os caminhos vêm de `shared/routing`: a navegação e as rotas não podem divergir.
export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [
      { index: true, element: <Home /> },
      { path: paths.configure, element: <Configure /> },
      { path: paths.engineeringRequest, element: <EngineeringRequest /> },
      {
        path: paths.settings,
        element: (
          <RequiresHost>
            <Settings />
          </RequiresHost>
        )
      },
      {
        path: executionPattern,
        element: (
          <RequiresHost>
            <Execution />
          </RequiresHost>
        )
      }
    ]
  }
];
