import { createHashRouter, RouterProvider } from 'react-router';

import { AppProviders } from '@/shared/components';
import '@/shared/styles/index.scss';

import { routes } from './routes';

// Roteador por hash: o aplicativo é servido pelo host Tauri, sem servidor que resolva caminhos.
const router = createHashRouter(routes);

export function Root() {
  return (
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  );
}
