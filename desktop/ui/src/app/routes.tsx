import { RequiresHost } from '@/shared/components';
import { executionPattern, paths } from '@/shared/routing';
import { Navigate, type RouteObject } from 'react-router';
import { WindowLayout, WindowLayoutError } from './layouts/window-layout';
import Agents from './routes/agents';
import Assistant from './routes/assistant';
import Configure from './routes/configure';
import EngineeringRequest from './routes/engineering-request';
import Execution from './routes/execution';
import Home from './routes/home';
import Integrations from './routes/integrations';
import Management from './routes/management';
import Security from './routes/security';
import Portfolio from './routes/portfolio';
import Project from './routes/project';
import Report from './routes/report';
import ProjectCatalog from './routes/project-catalog';
import Timeline from './routes/timeline';
import Audit from './routes/audit';
import ProjectHarness from './routes/project-harness';
import ProjectKnowledge from './routes/project-knowledge';
import Runs from './routes/runs';
import SettingsAi from './routes/settings-ai';
import SettingsDiagnostics from './routes/settings-diagnostics';

// Os caminhos vêm de `shared/routing`: a navegação e as rotas não podem divergir.
export const routes: RouteObject[] = [
  {
    path: paths.home,
    element: <WindowLayout />,
    errorElement: <WindowLayoutError />,
    children: [
      { index: true, element: <Home /> },
      {
        path: paths.project,
        element: (
          <RequiresHost>
            <Project />
          </RequiresHost>
        )
      },
      {
        path: paths.projectHarness,
        element: (
          <RequiresHost>
            <ProjectHarness />
          </RequiresHost>
        )
      },
      {
        path: paths.projectCatalog,
        element: (
          <RequiresHost>
            <ProjectCatalog />
          </RequiresHost>
        )
      },
      {
        path: paths.projectKnowledge,
        element: (
          <RequiresHost>
            <ProjectKnowledge />
          </RequiresHost>
        )
      },
      {
        path: paths.timeline,
        element: (
          <RequiresHost>
            <Timeline />
          </RequiresHost>
        )
      },
      {
        path: paths.audit,
        element: (
          <RequiresHost>
            <Audit />
          </RequiresHost>
        )
      },
      { path: paths.configure, element: <Configure /> },
      { path: paths.engineeringRequest, element: <EngineeringRequest /> },
      {
        path: paths.agents,
        element: (
          <RequiresHost>
            <Agents />
          </RequiresHost>
        )
      },
      {
        path: paths.security,
        element: (
          <RequiresHost>
            <Security />
          </RequiresHost>
        )
      },
      {
        path: paths.portfolio,
        element: (
          <RequiresHost>
            <Portfolio />
          </RequiresHost>
        )
      },
      {
        path: paths.assistant,
        element: (
          <RequiresHost>
            <Assistant />
          </RequiresHost>
        )
      },
      {
        path: paths.report,
        element: (
          <RequiresHost>
            <Report />
          </RequiresHost>
        )
      },
      {
        path: paths.management,
        element: (
          <RequiresHost>
            <Management />
          </RequiresHost>
        )
      },
      {
        path: paths.runs,
        element: (
          <RequiresHost>
            <Runs />
          </RequiresHost>
        )
      },
      {
        path: paths.integrations,
        element: (
          <RequiresHost>
            <Integrations />
          </RequiresHost>
        )
      },
      { path: paths.settings, element: <Navigate to={paths.settingsAi} replace /> },
      {
        path: paths.settingsAi,
        element: (
          <RequiresHost>
            <SettingsAi />
          </RequiresHost>
        )
      },
      { path: paths.settingsDiagnostics, element: <SettingsDiagnostics /> },
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
