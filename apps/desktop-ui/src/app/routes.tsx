import { paths } from '@/shared/routing';
import type { RouteObject } from 'react-router';
import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Configure from './routes/configure';
import Home from './routes/home';

export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [
      { index: true, element: <Home /> },
      { path: 'configurar', element: <Configure /> }
    ]
  }
];
