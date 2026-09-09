# Improvement recommendations

Gaps identified in the current reference app, grouped by area. Priority order: auth pattern + Hangfire dashboard auth first (missing on day one for anyone copying this template), then the startup-safety items (dev tooling exposure, auto-migration), then Dependabot/CodeQL/rate limiting/resilience.

## Security

- [ ] No authentication/authorization anywhere — add an `[Authorize]`-capable pattern (even a simple JWT/API-key scheme) so teams copying this template don't ship an open API by default.
- [ ] No rate limiting (`Microsoft.AspNetCore.RateLimiting` is built into ASP.NET Core, no extra dependency needed).
- [ ] Hangfire dashboard is enabled (`DashboardEnabled: true`) with no authorization filter — `src/WebApi/Configurations/HangfireConfiguration.cs` should require an authorization filter before exposing `/hangfire`.
- [ ] Default DB credentials live in plaintext in `src/WebApi/appsettings.json` — fine for a sample, but the README should steer real deployments to user-secrets/Key Vault.
- [ ] The Scalar API reference (`MapPlatformOpenApi`) and GraphQL Playground (`MapGeneratedGraphQlPlayground`) are mapped unconditionally in [Program.cs](src/WebApi/Program.cs), with no `IsDevelopment()` or config-toggle gate like Hangfire's `DashboardEnabled` — both are exposed in Production by default.

## Reliability

- [ ] No resilience policies around outbound calls (Hangfire jobs, DB) — `Microsoft.Extensions.Http.Resilience` (Polly-based, MS-supported) fits the AOT-friendly, source-generator-heavy style already used here.
- [ ] `UseDatabase()` in [DatabaseConfiguration.cs](src/WebApi/Configurations/DatabaseConfiguration.cs) calls `dbContext.Database.Migrate()` on every app startup, unconditionally and with no environment gate — scaling to multiple instances races them to apply migrations concurrently, and there's no separation between "apply schema" and "start serving traffic."

## CI/CD

- [ ] No code coverage collection/upload in CI.
- [ ] No Dependabot config for keeping the pinned packages in `src/Directory.Packages.props` current.
- [ ] No CodeQL/security scanning workflow.
- [ ] No workflow step builds/publishes the Docker image, even though `src/WebApi/Dockerfile` exists — add a `docker build` smoke-test job at minimum.

## API surface

- [ ] No API versioning strategy for the REST or GraphQL endpoints — decide now before there are real consumers.
