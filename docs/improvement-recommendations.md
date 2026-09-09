# Improvement recommendations

Gaps identified in the current reference app, grouped by area. Priority order: auth pattern + Hangfire dashboard auth + health checks first (missing on day one for anyone copying this template), then CI caching/Dependabot, then rate limiting/CORS/resilience.

## Security

- [ ] No authentication/authorization anywhere — add an `[Authorize]`-capable pattern (even a simple JWT/API-key scheme) so teams copying this template don't ship an open API by default.
- [x] No CORS policy configured.
- [ ] No rate limiting (`Microsoft.AspNetCore.RateLimiting` is built into ASP.NET Core, no extra dependency needed).
- [ ] Hangfire dashboard is enabled (`DashboardEnabled: true`) with no authorization filter — `src/WebApi/Configurations/HangfireConfiguration.cs` should require an authorization filter before exposing `/hangfire`.
- [ ] Default DB credentials live in plaintext in `src/WebApi/appsettings.json` — fine for a sample, but the README should steer real deployments to user-secrets/Key Vault.

## Reliability

- [x] No health check endpoints (`/health`, `/health/ready`) — add via `AddHealthChecks().AddNpgSql(...)` so orchestrators can probe the app.
- [ ] No resilience policies around outbound calls (Hangfire jobs, DB) — `Microsoft.Extensions.Http.Resilience` (Polly-based, MS-supported) fits the AOT-friendly, source-generator-heavy style already used here.

## CI/CD

- [x] `.github/workflows/build-and-test.yml` has no NuGet caching (`actions/setup-dotnet` supports `cache: true`).
- [ ] No code coverage collection/upload in CI.
- [ ] No Dependabot config for keeping the pinned packages in `src/Directory.Packages.props` current.
- [ ] No CodeQL/security scanning workflow.
- [ ] No workflow step builds/publishes the Docker image, even though `src/WebApi/Dockerfile` exists — add a `docker build` smoke-test job at minimum.

## API surface

- [ ] No API versioning strategy for the REST or GraphQL endpoints — decide now before there are real consumers.
- [ ] Verify `[ProducesResponseType]`/`ProblemDetails` coverage is consistent across features (OpenAPI/Scalar is present, but response documentation conventions weren't confirmed).

## Housekeeping

- [ ] `src/Directory.Build.props` had an in-progress uncommitted change (explanatory comments) as of this review — confirm it's committed or discarded.
