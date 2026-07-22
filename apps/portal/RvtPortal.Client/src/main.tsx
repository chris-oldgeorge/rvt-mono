// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-06-29 pending Loaded Leaflet core CSS before app map overrides for complete tile rendering.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import React from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import 'leaflet/dist/leaflet.css';
import './styles/app.css';

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
