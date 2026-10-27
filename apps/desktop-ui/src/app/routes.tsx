import type { RouteObject } from 'react-router';

import { paths } from '@/shared/routing';

import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Home from './routes/home';

/** Tabela de rotas do aplicativo. Os route modules em `routes/` são finos: só montam a feature. */
export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [{ index: true, element: <Home /> }]
  }
];
