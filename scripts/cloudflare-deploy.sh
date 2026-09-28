#!/usr/bin/env bash
# Deploys the read-only public WorkLens demo to Cloudflare: Angular assets + a Worker at
# the edge, and the .NET API (Demo mode, throwaway SQLite) in a Cloudflare Container.
# Used by .github/workflows/deploy-cloudflare.yml; also runnable locally.
#
# Required (unless VALIDATE_ONLY=true): CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID
# Optional:
#   APP_HOST        custom hostname (for example worklens.example.com); when empty the
#                   demo is served from its workers.dev URL only
#   VALIDATE_ONLY=true  build and run `wrangler deploy --dry-run` without contacting Cloudflare
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CF="$ROOT/cloudflare"
VALIDATE_ONLY="${VALIDATE_ONLY:-false}"
APP_HOST="${APP_HOST:-}"
export APP_HOST

if [ "$VALIDATE_ONLY" != "true" ]; then
  : "${CLOUDFLARE_API_TOKEN:?CLOUDFLARE_API_TOKEN is required}"
  : "${CLOUDFLARE_ACCOUNT_ID:?CLOUDFLARE_ACCOUNT_ID is required}"
fi

echo "==> Building Angular app"
npm ci --prefix "$ROOT/frontend"
npm run build --prefix "$ROOT/frontend" -- --configuration production

echo "==> Preparing Cloudflare Worker"
npm install --prefix "$CF"

node - "$CF/wrangler.template.jsonc" "$CF/wrangler.generated.jsonc" <<'NODE'
const fs = require("fs");
const [template, output] = process.argv.slice(2);
const config = JSON.parse(fs.readFileSync(template, "utf8"));
if (process.env.APP_HOST) {
  config.routes = [{ pattern: process.env.APP_HOST, custom_domain: true }];
}
fs.writeFileSync(output, JSON.stringify(config, null, 2) + "\n");
NODE

cd "$CF"
if [ "$VALIDATE_ONLY" = "true" ]; then
  npx wrangler deploy --dry-run --outdir "${RUNNER_TEMP:-/tmp}/wrangler-dry-run" --config wrangler.generated.jsonc
  echo "Cloudflare dry-run passed."
  exit 0
fi

DEPLOY_LOG="$(mktemp)"
npx wrangler deploy --config wrangler.generated.jsonc | tee "$DEPLOY_LOG"

if [ -n "$APP_HOST" ]; then
  APP_URL="https://$APP_HOST"
else
  APP_URL="$(grep -oE 'https://[a-z0-9.-]+\.workers\.dev' "$DEPLOY_LOG" | head -1)"
fi
: "${APP_URL:?could not determine the deployed URL}"

wait_for() {
  url="$1"
  pattern="$2"
  for attempt in $(seq 1 18); do
    body="$(curl --fail --silent --show-error --retry 2 --retry-delay 2 --retry-all-errors "$url" 2>/dev/null || true)"
    if printf '%s' "$body" | grep -q "$pattern"; then
      return 0
    fi
    echo "Waiting for $url (attempt $attempt/18)..."
    sleep 10
  done
  echo "::error::Smoke test failed: $url"
  return 1
}

echo "==> Smoke testing $APP_URL"
wait_for "$APP_URL/api/health" '"database":true'
wait_for "$APP_URL/" "<app-root"
wait_for "$APP_URL/api/demo" '"enabled":true'
wait_for "$APP_URL/api/applications" "Northwind Logistics"

# The demo is shared by every visitor, so it must reject writes.
code="$(curl --silent --output /dev/null --write-out '%{http_code}' -X POST \
  -H 'Content-Type: application/json' -d '{}' "$APP_URL/api/applications")"
if [ "$code" != "403" ]; then
  echo "::error::Expected the public demo to reject writes with 403, got HTTP $code."
  exit 1
fi
echo "Writes are rejected (read-only demo)."

echo "Deployed: $APP_URL"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "WorkLens public demo deployed: $APP_URL" >> "$GITHUB_STEP_SUMMARY"; fi
