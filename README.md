# golden-path-aspnet

A .NET 10 ASP.NET Core minimal API reference application — a "golden path" showing how a
production-shaped service fits together: source-generated endpoint/service/config registration,
REST and GraphQL over the same feature handlers, EF Core on PostgreSQL, background jobs, OpenTelemetry,
and GitHub-based auth in front of all of it.

The example domain is a small Todo API (create/get/list/update/toggle/delete, plus scheduled
reminder and sweep jobs), which exists to exercise the plumbing rather than as a feature in itself.

## Architecture

- `src/WebApi` — the application: feature handlers, database context and migrations, JSON
  configuration, and startup wiring. Endpoints, services, configuration, and telemetry are
  registered via Roslyn source generators rather than hand-written `Startup`/`Program` boilerplate.
- `src/WebApi.Platform` — reusable cross-cutting setup (telemetry, OpenAPI, exception handling)
  shared by the application.
- `src/Generators/Generators` — the source generators consumed as analyzers by the projects above.
- `src/WebApi.Tests` — TUnit integration tests that run the API in-memory against a real
  PostgreSQL via Testcontainers, exercised through its public HTTP interface.
- `src/Benchmarks` — BenchmarkDotNet microbenchmarks.
- `k6/` — k6 load tests for the REST and GraphQL transports, used to catch performance regressions
  (see [k6/README.md](k6/README.md)).
- `deploy/mimir` — deployment manifests for the operator's Kubernetes homelab platform ("Mimir"),
  which this service runs on.

Each feature (e.g. `src/WebApi/Features/Todos/CreateTodo`) is a self-contained vertical slice:
request/response DTOs, a FluentValidation validator, and a handler exposed over both REST and
GraphQL from the same code.

Every request — REST endpoints, GraphQL, the Hangfire dashboard, and the Scalar/OpenAPI docs —
requires a GitHub login, as described below.

## Running locally

`compose.yaml` runs the app's dependencies only (Postgres, the OTel collector, the Aspire
dashboard) — the app itself runs on the host:

```bash
docker compose up -d
dotnet run --project src/WebApi
```

The app listens on `http://localhost:5200` by default. The Aspire dashboard (logs/traces/spans) is
at `http://localhost:18888`.

Complete the [GitHub OAuth App setup](#one-time-setup-register-a-github-oauth-app) below before
logging in for the first time.

### Common commands

```bash
dotnet restore src/GoldenPathAspnet.slnx
dotnet build src/GoldenPathAspnet.slnx --no-restore
dotnet test --solution src/GoldenPathAspnet.slnx --no-build
dotnet run --project src/WebApi/WebApi.csproj
```

Integration tests require Docker (Testcontainers starts PostgreSQL for them). See
[AGENTS.md](AGENTS.md) for fuller contributor conventions.

## Authentication

Every request into the service — REST endpoints, GraphQL, the Hangfire dashboard, and the
Scalar/OpenAPI docs — requires a GitHub login, restricted to the GitHub account configured as
`Authentication:AllowedGitHubLogin` in [`appsettings.json`](src/WebApi/appsettings.json).

### One-time setup: register a GitHub OAuth App

GitHub doesn't expose an API for creating OAuth Apps, so this step is manual:

1. Go to https://github.com/settings/applications/new (or your org's equivalent under
   `https://github.com/organizations/<org>/settings/applications/new` if you'd rather own it there).
2. Fill in:
   - **Application name**: anything, e.g. `golden-path-aspnet (local)`
   - **Homepage URL**: `http://localhost:5200`
   - **Authorization callback URL**: `http://localhost:5200/auth/callback/github`
3. Click **Register application**, then **Generate a new client secret**.
4. Store the Client ID and Client Secret locally with `dotnet user-secrets` — never commit them to
   `appsettings.json`. The `--project` flag lets you run this from the repo root; without it,
   `dotnet user-secrets` must be run from inside `src/WebApi`:

   ```bash
   dotnet user-secrets set "Authentication:GitHub:ClientId" "<client-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:GitHub:ClientSecret" "<client-secret>" --project src/WebApi
   ```

For a deployed environment, register a second OAuth App with that environment's real callback URL
and supply the same two values as environment variables
(`Authentication__GitHub__ClientId` / `Authentication__GitHub__ClientSecret`) instead of user secrets.

### Local development

`compose.yaml` only runs the app's dependencies (Postgres, the OTel collector, the Aspire dashboard) —
the app itself runs on the host via `dotnet run`, using the `dotnet user-secrets` values from setup
above and the `localhost`-pointed defaults already in `appsettings.json`:

```bash
docker compose up -d
dotnet run --project src/WebApi
```

### How it works

- Logging in (`/login`) redirects to GitHub, then back to `/auth/callback/github`. The ticket is rejected
  unless the GitHub login matches `Authentication:AllowedGitHubLogin`, so authenticating with GitHub
  proves identity but a non-matching account still gets bounced to `/access-denied`.
- A global authorization fallback policy requires an authenticated session for any endpoint that
  doesn't explicitly opt out — only `/login`, `/logout`, and `/access-denied` are anonymous.
- `POST /logout` clears the session cookie.
