import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { PrototypeApp } from './prototype-app';
import './styles/base.scss';

// Entrada do protótipo visual (prototype.html). Não faz parte do build de produção: só o index.html é empacotado.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PrototypeApp />
  </StrictMode>
);
