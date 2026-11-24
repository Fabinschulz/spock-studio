import { LoadingState, RequiresHost } from '@/shared/components';
import { executionPattern, paths } from '@/shared/routing';
import { lazy, Suspense, type ComponentType } from 'react';
import { Navigate, Outlet, type RouteObject } from 'react-router';
import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Home from './routes/home';

// A tela inicial vem no pacote principal; as outras são baixadas na primeira vez que a pessoa abre cada uma.
const page = (load: () => Promise<{ default: ComponentType }>) => {
  const Page = lazy(load);
  return <Page />;
};

/** As telas que falam com o motor: no navegador (prévia), todas dizem que só funcionam no aplicativo. */
function HostOnly() {
  return (
    <RequiresHost>
      <Outlet />
    </RequiresHost>
  );
}

function Pages() {
  return (
    <Suspense fallback={<LoadingState>Abrindo a tela…</LoadingState>}>
      <Outlet />
    </Suspense>
  );
}

// Os caminhos vêm de `shared/routing`: a navegação e as rotas não podem divergir.
export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [
      { index: true, element: <Home /> },
      {
        element: <Pages />,
        children: [
          { path: paths.configure, element: page(() => import('./routes/configure')) },
          { path: paths.engineeringRequest, element: page(() => import('./routes/engineering-request')) },
          { path: paths.settings, element: <Navigate to={paths.settingsAi} replace /> },
          { path: paths.settingsDiagnostics, element: page(() => import('./routes/settings-diagnostics')) },
          { path: paths.settingsAbout, element: page(() => import('./routes/settings-about')) },
          {
            element: <HostOnly />,
            children: [
              { path: paths.project, element: page(() => import('./routes/project')) },
              { path: paths.projectHarness, element: page(() => import('./routes/project-harness')) },
              { path: paths.projectCatalog, element: page(() => import('./routes/project-catalog')) },
              { path: paths.projectKnowledge, element: page(() => import('./routes/project-knowledge')) },
              { path: paths.projectArchitecture, element: page(() => import('./routes/project-architecture')) },
              { path: paths.timeline, element: page(() => import('./routes/timeline')) },
              { path: paths.audit, element: page(() => import('./routes/audit')) },
              { path: paths.agents, element: page(() => import('./routes/agents')) },
              { path: paths.idea, element: page(() => import('./routes/idea')) },
              { path: paths.security, element: page(() => import('./routes/security')) },
              { path: paths.portfolio, element: page(() => import('./routes/portfolio')) },
              { path: paths.assistant, element: page(() => import('./routes/assistant')) },
              { path: paths.report, element: page(() => import('./routes/report')) },
              { path: paths.management, element: page(() => import('./routes/management')) },
              { path: paths.runs, element: page(() => import('./routes/runs')) },
              { path: paths.integrations, element: page(() => import('./routes/integrations')) },
              { path: paths.cloud, element: page(() => import('./routes/cloud')) },
              { path: paths.infrastructure, element: page(() => import('./routes/infrastructure')) },
              { path: paths.connectionsModels, element: page(() => import('./routes/settings-ai')) },
              { path: paths.settingsAi, element: page(() => import('./routes/settings-ai')) },
              { path: executionPattern, element: page(() => import('./routes/execution')) }
            ]
          }
        ]
      }
    ]
  }
];
