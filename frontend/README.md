# Frontend

Angular 22 (standalone components, built-in control flow, lazy-loaded routes) built
with the Angular CLI's application builder. See the [root README](../README.md) for
running the API and the whole stack.

| Command | What it does |
|---|---|
| `npm start` | Dev server on http://localhost:4200, proxying `/api` to the API (see `proxy.conf.json`) |
| `npm test` | Unit tests with Vitest (`ng test`) |
| `npm run build` | Production build into `dist/frontend/browser` |

Requires Node 22.22.3+ or 24.15+ (see `engines` in `package.json`).
