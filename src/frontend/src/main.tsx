import { setupListeners } from '@reduxjs/toolkit/query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router';
import { SessionBootstrap } from './features/auth/SessionBootstrap';
import { AppRoutes } from './app/router';
import { makeStore } from './app/store';
import { initTheme } from './shared/theme/theme';
// Fonts are bundled with the app (never fetched from a font CDN); Latin subsets.
import '@fontsource/inter/latin-400.css';
import '@fontsource/inter/latin-500.css';
import '@fontsource/inter/latin-600.css';
import '@fontsource/inter/latin-700.css';
import '@fontsource/jetbrains-mono/latin-400.css';
import '@fontsource/jetbrains-mono/latin-500.css';
import './index.css';

initTheme();
const store = makeStore();
// Lets queries that opt in refetch on window focus and reconnect (spec 003 research R12).
setupListeners(store.dispatch);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <BrowserRouter>
        <SessionBootstrap>
          <AppRoutes />
        </SessionBootstrap>
      </BrowserRouter>
    </Provider>
  </StrictMode>,
);
