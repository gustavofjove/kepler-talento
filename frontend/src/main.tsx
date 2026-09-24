import '@fontsource/montserrat/600.css';
import '@fontsource/montserrat/700.css';
import '@fontsource/nunito-sans/400.css';
import '@fontsource/nunito-sans/500.css';
import '@fontsource/nunito-sans/600.css';
import './styles.css';
// Synchronous init with bundled resources: copy is ready before the first render.
import './app/core/i18n/i18n';

import { createRoot } from 'react-dom/client';
import { I18nProvider } from 'react-aria-components';
import { RouterProvider } from 'react-router/dom';
import { createAppRouter } from './app/app';
import { appNavigator } from './app/core/di/services';
import { evictSupersededStorage } from './app/core/storage/evict-legacy-storage';

// First, before the router and before any API call: candidate data that earlier builds
// left in this browser goes whether or not the backend is reachable.
evictSupersededStorage();

const router = createAppRouter();
// Lets AuthService.signOut() redirect from outside React.
appNavigator.attach(router);

const container = document.getElementById('root');
if (!container) {
  throw new Error('No se ha encontrado el elemento raíz de la aplicación.');
}

// Deliberately no <StrictMode>: React 19 double-invokes effects in development,
// which would double-fire toasts and diverge from the Angular behaviour this
// port must reproduce. Enabling it is tracked as a follow-up.
// react-aria (catalog value picker) localizes its few internal strings from this locale.
createRoot(container).render(
  <I18nProvider locale="es-ES">
    <RouterProvider router={router} />
  </I18nProvider>,
);
