import { setupListeners } from '@reduxjs/toolkit/query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router';
import { SessionBootstrap } from './features/auth/SessionBootstrap';
import { AppRoutes } from './app/router';
import { makeStore } from './app/store';
import './index.css';

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
