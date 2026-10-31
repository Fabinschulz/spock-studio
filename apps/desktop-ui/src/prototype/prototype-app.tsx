import { Provider } from '@synthra.io/ui-kit';
import { RouterProvider } from 'react-router';

import { router } from './router';
import { prototypeTheme } from './theme/prototype-theme';

export function PrototypeApp() {
  return (
    <Provider theme={prototypeTheme} cssBaseline={false}>
      <div className="mz">
        <RouterProvider router={router} />
      </div>
    </Provider>
  );
}
