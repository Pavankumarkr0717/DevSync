import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { BrowserRouter } from 'react-router';
import { SessionBootstrap } from './features/auth/SessionBootstrap';
import { AppRoutes } from './app/router';
import { makeStore } from './app/store';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={makeStore()}>
      <BrowserRouter>
        <SessionBootstrap>
          <AppRoutes />
        </SessionBootstrap>
      </BrowserRouter>
    </Provider>
  </StrictMode>,
);
