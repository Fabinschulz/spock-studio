import { Workbench } from '@/prototype/components/workbench';
import { ConfigureScreen } from '@/prototype/screens/configure';
import { HomeScreen } from '@/prototype/screens/home';
import { ProjectScreen } from '@/prototype/screens/project';
import { createHashRouter, Navigate } from 'react-router';

export const router = createHashRouter([
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
