# golden-path-aspnet

A .NET 10 ASP.NET Core minimal API reference application — a "golden path" showing how a
production-shaped service fits together: source-generated endpoint/service/config registration,
REST and GraphQL over the same feature handlers, EF Core on PostgreSQL, background jobs, OpenTelemetry,
and Google/Facebook/Apple-based auth in front of all of it.

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
requires an authenticated session, as described below.

## Running locally

`compose.yaml` runs the app's dependencies only (Postgres, the OTel collector, the Aspire
dashboard) — the app itself runs on the host:

```bash
docker compose up -d
dotnet run --project src/WebApi
```

The app listens on `http://localhost:5200` by default. The Aspire dashboard (logs/traces/spans) is
at `http://localhost:18888`.

Complete the [Google OAuth setup](#one-time-setup-register-a-google-oauth-client), the
[Facebook Login setup](#one-time-setup-register-a-facebook-login-app), and/or the
[Sign in with Apple setup](#one-time-setup-register-a-sign-in-with-apple-service-id) below before
logging in for the first time. Apple's redirect URI must be a publicly-reachable HTTPS domain (see
that section), so Google or Facebook are the simpler choice for local development.

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
Scalar/OpenAPI docs — requires an authenticated session.

Google is the only provider wired up today - `Program.cs` calls `AddGoogleAuthentication()` and
leaves `AddFacebookAuthentication()`/`AddAppleAuthentication()` commented out. Each provider is its
own extension method in `AuthenticationConfiguration`, so a provider is "enabled" purely by whether
Program.cs calls it: calling one commits to that provider being fully configured and throws
immediately at startup if a required setting below was left blank, rather than registering it
half-configured. To turn on Facebook or Apple once their credentials exist, uncomment its call in
`Program.cs`. Until then, `/login?provider=facebook` or `?provider=apple` returns
`400 Bad Request` instead of challenging it.

### One-time setup: register a Google OAuth Client

1. Go to the [Google Cloud Console Credentials page](https://console.cloud.google.com/apis/credentials).
2. Create an OAuth 2.0 Client ID (Web application type).
3. Set Authorized redirect URIs:
   - For local development: `http://localhost:5200/auth/callback/google`
4. Store the Client ID and Client Secret locally with `dotnet user-secrets` — never commit them to
   `appsettings.json`:

   ```bash
   dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>" --project src/WebApi
   ```

For a deployed environment, supply the values as environment variables
(`Authentication__Google__ClientId` / `Authentication__Google__ClientSecret`).

### One-time setup: register a Facebook Login app (optional)

1. Go to the [Meta for Developers apps page](https://developers.facebook.com/apps/) and create an
   app with the Facebook Login product added.
2. Under Facebook Login settings, set Valid OAuth Redirect URIs:
   - For local development: `http://localhost:5200/auth/callback/facebook`
3. Store the App ID and App Secret locally with `dotnet user-secrets` — never commit them to
   `appsettings.json`:

   ```bash
   dotnet user-secrets set "Authentication:Facebook:ClientId" "<app-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:Facebook:ClientSecret" "<app-secret>" --project src/WebApi
   ```

For a deployed environment, supply the values as environment variables
(`Authentication__Facebook__ClientId` / `Authentication__Facebook__ClientSecret`).

### One-time setup: register a Sign in with Apple Service ID (optional)

Sign in with Apple has more moving parts than Google/Facebook: instead of a static client secret,
Apple requires a JWT signed with an EC private key, regenerated per token exchange (handled for us
by [`AspNet.Security.OAuth.Apple`](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers)).

1. In the [Apple Developer portal](https://developer.apple.com/account/resources/identifiers/list),
   register an App ID with the "Sign in with Apple" capability enabled, then create a **Services ID**
   — this Services ID (e.g. `com.example.goldenpath.signin`), not the app's bundle ID, is the
   `ClientId`.
2. Configure that Services ID's "Sign in with Apple" settings with your domain and a Return URL:
   - Apple does **not** accept `http://localhost` as a Return URL — it must be a publicly-reachable
     HTTPS domain (e.g. via a tunnel like `ngrok` for local testing, or your deployed domain):
     `https://<your-domain>/auth/callback/apple`
3. Under Keys, create a new key with "Sign in with Apple" enabled and download the resulting
   `AuthKey_<KeyId>.p8` file — **Apple only lets you download it once**. Note the Key ID (from the
   filename) and your Team ID (top-right of the developer portal).
4. Store the Services ID, Team ID, Key ID, and the private key's raw file contents locally with
   `dotnet user-secrets` — never commit them to `appsettings.json`:

   ```bash
   dotnet user-secrets set "Authentication:Apple:ClientId" "<services-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:Apple:TeamId" "<team-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:Apple:KeyId" "<key-id>" --project src/WebApi
   dotnet user-secrets set "Authentication:Apple:PrivateKey" "$(cat AuthKey_<key-id>.p8)" --project src/WebApi
   ```

For a deployed environment, supply the values as environment variables
(`Authentication__Apple__ClientId` / `Authentication__Apple__TeamId` / `Authentication__Apple__KeyId`
/ `Authentication__Apple__PrivateKey`, the last carrying the `.p8` file's contents verbatim,
newlines included).

### How it works

- Logging in (`/login`) redirects to Google OAuth by default, then back to
  `/auth/callback/google`. Pass `/login?provider=facebook` or `/login?provider=apple` to sign in
  with Facebook or Apple instead (redirecting back to `/auth/callback/facebook` or
  `/auth/callback/apple` respectively) — provided that provider's `Add{Provider}Authentication` call
  is uncommented in `Program.cs`; otherwise the request is rejected the same way an unrecognized
  `provider` value is.
- Apple only ever sends the user's name once, on the very first authorization for a given app — it
  arrives as a `user` form field alongside the callback rather than as a token claim, and is parsed
  in `AuthenticationConfiguration.ParseAppleUserName`. Subsequent Apple logins carry no name at all.
- A global authorization fallback policy requires an authenticated session for any endpoint that
  doesn't explicitly opt out — only `/login`, `/logout`, and `/access-denied` are anonymous.
- `POST /logout` clears the session cookie.
