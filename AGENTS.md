# AGENTS.md

## Project overview

This repository is a .NET 10 ASP.NET Core minimal API reference application: REST (OpenAPI), GraphQL and gRPC over the same handlers. It uses PostgreSQL via EF Core, Hangfire, FluentValidation (validators are generated from the OpenAPI contracts), OpenTelemetry, and Roslyn source generators for endpoint, validator, service, configuration, and telemetry registration. Contracts are written by hand first; generated code and the compiler hold the implementation to them.

## Repository layout

- `src/WebApi` — the application: feature handlers, database context, migrations, and API startup.
  - `Contracts/OpenApi`, `Contracts/GraphQL`, `Contracts/Grpc` — the hand-written contracts (source of truth).
  - `Features/<Area>/<Operation>` — handler, REST endpoint and GraphQL resolver per operation; one gRPC service per resource.
  - `Configurations/` — cross-cutting setup, one static class per concern (authentication, CORS, forwarded headers, exception handling, health checks, telemetry, Hangfire, OpenAPI docs, gRPC, database), called from `Program.cs`.
- `src/WebApi.Tests` — TUnit integration tests using Testcontainers PostgreSQL.
- `src/Generators/Generators` — source generators consumed as analyzers by `WebApi`; `src/Generators/Generators.Tests` tests them.
- `src/Benchmarks` — BenchmarkDotNet microbenchmarks.
- `k6/` — load-test scripts for REST and GraphQL (not gRPC yet).
- `src/GoldenPathAspnet.slnx` — solution entry point.

## Prerequisites

- Use the SDK pinned in `src/global.json` (currently .NET 10).
- Run `dotnet` commands from `src/`: `global.json` (SDK pin and the Microsoft.Testing.Platform runner setting that `dotnet test --solution` needs) lives there, and the paths below are relative to it.
- Docker must be available to run the integration tests because they start PostgreSQL with Testcontainers.

## Common commands

```bash
dotnet restore GoldenPathAspnet.slnx
dotnet build GoldenPathAspnet.slnx --no-restore
dotnet test --solution GoldenPathAspnet.slnx --no-build
dotnet run --project WebApi/WebApi.csproj
dotnet tool restore --tool-manifest dotnet-tools.json
```

Run `dotnet format` only when intentionally applying repository-wide formatting; avoid unrelated formatting churn.

## Implementation conventions

- Keep nullable reference types enabled and resolve all compiler warnings: `WebApi` treats warnings as errors.
- Start from the contract. A new operation is added to `Contracts/OpenApi/*.openapi.yaml` and `Contracts/GraphQL/*.graphql` (and `Contracts/Grpc/**/*.proto` if it is exposed over gRPC), then built: the generators report every missing implementation as a build error (warnings are errors).
- Implement an operation in `src/WebApi/Features/<Area>/<Operation>`: a `[Service]` handler holding the logic, a `<Operation>Endpoint : I<Operation>Endpoint` (REST) and a `<Operation>Resolver : I<Operation>Resolver` (GraphQL). The interface names and request/response types come from the generators; do not hand-write DTOs or registrations they produce. For gRPC, override the RPC in the resource's `*GrpcService` (the generated base answers `UNIMPLEMENTED`, so `GrpcTests.EveryRpcInTheContract_IsImplemented` is what catches a forgotten one).
- Adapters (endpoint, resolver, gRPC service) only map types and translate "not found"/invalid input into their transport's documented response. Business logic belongs in the handler.
- Validation rules are generated from the OpenAPI schema constraints (`minLength`, `pattern`, `minimum`, `maximum`, required); change the contract, not a hand-written validator. All transports use the same generated validators. Cover invalid input in integration tests.
- gRPC errors: a failed validation is `INVALID_ARGUMENT` with a `google.rpc.BadRequest` detail and a missing entity is `NOT_FOUND` (see `Services/GrpcErrors.cs`); never put internal detail into a status message. Keep proto changes backwards compatible: never reuse or renumber a field, `reserved` removed ones.
- Authentication is deny-by-default: the global fallback policy covers every endpoint, gRPC included. Anything anonymous needs `AllowAnonymous` and an entry in the pinned list in `RouteAuthorizationTests` (HTTP) or `GrpcConfiguration.AnonymousMethods` (gRPC).
- Use `TodoContext` and EF Core migrations for schema changes. Do not edit generated migration designer or model-snapshot files unless the EF tooling produces the change.
- Use the generated application-configuration APIs rather than ad-hoc configuration binding where a generated option is available.
- Prefer source generation over reflection (registration, serialization, configuration). The app is **not** published with native AOT today (the Dockerfile publishes a framework-dependent JIT build, and EF Core and Hangfire are used as-is), so this is about keeping that option open, not a current guarantee.

## Testing approach

- Treat the running API as the preferred test unit: use the startup fixture to run it in memory, backed by real PostgreSQL through Testcontainers, and exercise it through its public HTTP interface. The unit should be as large and black-box as practical.
- Add or update a test in `src/WebApi.Tests` for user-visible behavior, including invalid input. gRPC is tested in `GrpcTests` through generated client stubs against the in-memory server.
- Use focused unit tests only for isolated logic that is genuinely simpler and more valuable to test directly. Do not create tests that mock many dependencies or require intricate setup; cover that behavior through the black-box fixture instead.
- Verify the affected project at minimum; run the solution test suite for changes to shared infrastructure, generators, database behavior, or public API contracts. When changing a source generator, build both it and its consumer projects.

## Runtime diagnostics

- For runtime failures, inspect telemetry before changing code. The Compose environment exports application logs, traces, and spans to the Aspire Dashboard at `http://localhost:18888`.
- The Aspire CLI (`aspire`) reads this telemetry directly from the terminal, without opening the dashboard UI — [install it](https://aka.ms/aspire/install-cli) if the `aspire` command is not available.
- Use `aspire otel logs --dashboard-url http://localhost:18888`, `aspire otel traces --dashboard-url http://localhost:18888`, and `aspire otel spans --dashboard-url http://localhost:18888` to view structured logs, traces, and spans respectively. Add `--non-interactive` when running from an agent/script context to avoid interactive prompts.
- Filter with options such as `--has-error` (traces/spans with errors) and `--trace-id <trace-id>` (logs/spans for a specific trace) to narrow results, e.g. `aspire otel traces --dashboard-url http://localhost:18888 --has-error --non-interactive` and `aspire otel logs --dashboard-url http://localhost:18888 --trace-id <trace-id> --non-interactive`. A trace ID returned in a problem response can therefore be used to retrieve the corresponding exception details.
- Use `aspire logs <resource>` when the application is managed by an Aspire AppHost; otherwise use the `aspire otel` commands above for this repository's Compose-based dashboard.
- Use `aspire export --dashboard-url http://localhost:18888` to save a zip snapshot of the current telemetry for offline inspection or sharing.

## Deployment (Mimir)

This repo runs on the operator's Kubernetes homelab platform, "Mimir." Deployment manifests live in `deploy/mimir` (`services.test.yaml`, `services.prod.yaml`).

## Change hygiene

- Keep changes scoped to the requested behavior.
- Do not modify generated files by hand.
- Do not commit secrets or local connection strings.
- Before handing off, report the commands run and any checks that could not run (for example, Docker/Testcontainers unavailable).
