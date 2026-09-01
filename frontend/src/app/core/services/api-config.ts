/**
 * Resolves the backend API base URL at runtime instead of baking it in at build time.
 *
 * On-prem deployments rarely know their final hostname/port at build time (reverse
 * proxy, internal DNS name, different port per environment, etc.), so this reads an
 * optional `window.__WORKLENS_API_BASE__` global that `env.js` (served alongside
 * index.html, see nginx config) can set per-deployment without rebuilding the app.
 * Falls back to a relative `/api` path, which works when nginx proxies /api/* to the
 * backend container — the recommended setup in docker-compose.yml.
 */
export function resolveApiBase(): string {
  const w = window as unknown as { __WORKLENS_API_BASE__?: string };
  return w.__WORKLENS_API_BASE__?.replace(/\/$/, '') || '/api';
}
