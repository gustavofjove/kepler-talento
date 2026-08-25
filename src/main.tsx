import '@fontsource/montserrat/600.css';
import '@fontsource/montserrat/700.css';
import '@fontsource/nunito-sans/400.css';
import '@fontsource/nunito-sans/500.css';
import '@fontsource/nunito-sans/600.css';
import './styles.css';

import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router/dom';
import { createAppRouter } from './app/app';
import { appNavigator } from './app/core/di/services';

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
createRoot(container).render(<RouterProvider router={router} />);
