import { Container } from "@cloudflare/containers";

export interface Env {
  ASSETS: Fetcher;
  API: DurableObjectNamespace<WorkLensDemoApi>;
}

// The hosted WorkLens is always the read-only public demo: a throwaway SQLite database
// seeded with fictional tracker data, live public job feeds, and no Outlook or OpenAI.
export class WorkLensDemoApi extends Container<Env> {
  defaultPort = 8080;
  sleepAfter = "15m";
  pingEndpoint = "localhost/api/health";
  envVars = {
    ASPNETCORE_ENVIRONMENT: "Production",
    Demo__Enabled: "true",
    Demo__DatabasePath: "/tmp/worklens-demo.db",
    Swagger__Enabled: "false",
  };
}

const api = (env: Env) => env.API.getByName("api");

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname.startsWith("/api/")) {
      return api(env).fetch(request);
    }
    return env.ASSETS.fetch(request);
  },
} satisfies ExportedHandler<Env>;
