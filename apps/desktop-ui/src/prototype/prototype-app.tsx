import { Provider } from '@synthra.io/ui-kit';
import { createHashRouter, Navigate, RouterProvider } from 'react-router';

import { ConfigureScreen } from './screens/configure/configure-screen';
import { HomeScreen } from './screens/home';
import { ProjectScreen } from './screens/project';
import { Workbench } from './shell/workbench';
import { prototypeTheme } from './theme';

const router = createHashRouter([
  {
    element: <Workbench />,
    children: [
      { index: true, element: <HomeScreen /> },
      { path: 'projeto', element: <ProjectScreen /> },
      { path: 'configurar', element: <Navigate to="/configurar/projeto" replace /> },
      { path: 'configurar/:step', element: <ConfigureScreen /> }
    ]
  }
]);

export function PrototypeApp() {
  return (
    <Provider theme={prototypeTheme} cssBaseline={false}>
      <div className="mz">
        <RouterProvider router={router} />
      </div>
    </Provider>
  );
}
