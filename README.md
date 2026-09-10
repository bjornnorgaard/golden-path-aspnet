# golden-path-aspnet

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
