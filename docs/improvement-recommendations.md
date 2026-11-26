# Improvement recommendations

Gaps in the current reference app, grouped by area. Items are checked against the code (last review:
when gRPC was added); finished ones stay listed, marked `[x]`, so it is clear what changed since the
original list.

## Security

- [x] ~~No authentication/authorization anywhere~~ — done: cookie sessions with Google sign-in (Facebook and Apple are implemented but not enabled in `Program.cs`) and a global fallback policy that requires an authenticated user for every endpoint (REST, GraphQL, gRPC, Hangfire dashboard, docs) except the explicitly anonymous ones. Pinned by `RouteAuthorizationTests` and `GrpcTests`.
- [ ] **Authorization beyond "signed in".** Any Google account that completes the login is provisioned as a user and gets a session (`AuthenticationConfiguration.UpsertUserAsync`); there is no allow-list, role or ownership check, so every signed-in user can read and change every todo and user, and open the Hangfire dashboard. Decide the intended model (allow-listed domains/emails, roles, per-owner access) before copying this template.
- [ ] **Non-browser clients.** The only scheme is a browser cookie session, which suits neither gRPC clients nor service-to-service REST calls. Add a bearer/API-key scheme (the fallback policy already accepts any authentication scheme that succeeds).
- [ ] No rate limiting (`Microsoft.AspNetCore.RateLimiting` is built into ASP.NET Core, no extra dependency needed).
- [x] ~~Hangfire dashboard has no authorization filter~~ — it is behind the fallback policy, and mapped only when `Hangfire:DashboardEnabled` is true. (It is still open to any signed-in user, see the authorization item above.)
- [ ] Default DB credentials live in plaintext in `src/WebApi/appsettings.json` — fine for a sample, but the README should steer real deployments to user-secrets/Key Vault.
- [ ] The Scalar API reference (`MapPlatformOpenApi`) and the GraphQL playground are mapped unconditionally in [Program.cs](../src/WebApi/Program.cs), with no `IsDevelopment()` or config-toggle gate like Hangfire's `DashboardEnabled`. They require authentication, but are exposed in Production by default. gRPC reflection, by contrast, is opt-in (`Grpc:ReflectionEnabled`).

## Reliability

- [ ] No resilience policies around outbound calls (Hangfire jobs, DB) — `Microsoft.Extensions.Http.Resilience` (Polly-based, MS-supported) fits the source-generator-heavy style already used here.
- [~] `UseDatabase()` in [DatabaseConfiguration.cs](../src/WebApi/Configurations/DatabaseConfiguration.cs) migrates on every startup. Concurrent instances no longer race (the migration runs under a Postgres advisory lock), but there is still no environment gate and no separation between "apply schema" and "start serving traffic".
- [ ] Background jobs are scheduled through a second client after the request's own `SaveChanges` (for example `CreateTodoHandler`), so a crash between the two leaves a todo without its reminder, and the two use separate connections. Enqueue in the same transaction as the write (check whether Hangfire.PostgreSql's transaction-enlistment options fit), or use a transactional outbox.

## API surface

- [ ] No API versioning strategy for the REST or GraphQL endpoints — decide now before there are real consumers. (The gRPC contract is already versioned in its package, `goldenpath.*.v1`.)
- [ ] Contract breaking-change checks in CI: nothing compares the OpenAPI, GraphQL or `.proto` contracts against `main` (golden-path-go runs `oasdiff` and `buf breaking`).
- [ ] The REST and GraphQL todo/user list operations disagree on negative offsets (users clamp to 0, todos are rejected by the contract's `minimum`). Pick one rule and put it in the contract.

## CI/CD

- [ ] No code coverage collection/upload in CI.
- [ ] No Dependabot config for keeping the pinned packages in `src/Directory.Packages.props` current.
- [ ] No CodeQL/security scanning workflow.
- [x] ~~No workflow step builds/publishes the Docker image~~ — done: the `image` job in `build-and-test.yml` builds it (and pushes on `main`).
- [ ] gRPC is not part of the k6 load tests or of the Mimir manifests (no gRPC port is declared there yet).

## Tests

- [ ] `TestAuthHandler` inserts the shared test user on every authenticated request; with many hosts running in parallel this occasionally fails with a `duplicate key value violates unique constraint "IX_Users_Email"` (seen once in a full run). Seed the user once per database instead.
- [ ] Several assertions match FluentValidation's English messages, so the suite fails on a machine with a non-English culture (it passes with `LANG=en_US.UTF-8 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`). Pin the culture for the test run or assert on the property name only.
