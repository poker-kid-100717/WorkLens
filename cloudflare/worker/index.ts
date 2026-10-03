import { Container } from "@cloudflare/containers";

export interface Env {
  ASSETS: Fetcher;
  API: DurableObjectNamespace<WorkLensDemoApi>;
  DATABASE_URL?: string;
}

// The hosted WorkLens is always the read-only public demo: fictional tracker data, live
// public job feeds, and no Outlook or OpenAI. It runs on SQL Server when DATABASE_URL is set
// (for example Azure SQL Database) and on a throwaway SQLite file otherwise.
export class WorkLensDemoApi extends Container<Env> {
  defaultPort = 8080;
  sleepAfter = "15m";
  pingEndpoint = "localhost/api/health";

  constructor(ctx: DurableObjectState<{}>, env: Env) {
    super(ctx, env);
    this.envVars = {
      ASPNETCORE_ENVIRONMENT: "Production",
      Demo__Enabled: "true",
      Demo__DatabasePath: "/tmp/worklens-demo.db",
      ...(env.DATABASE_URL ? { Demo__ConnectionString: env.DATABASE_URL } : {}),
      Swagger__Enabled: "false",
    };
  }
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
