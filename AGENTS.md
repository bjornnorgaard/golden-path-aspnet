# AGENTS.md

## Project overview

This repository is a .NET 10 ASP.NET Core minimal API reference application. It uses PostgreSQL via EF Core, FluentValidation, OpenTelemetry, and Roslyn source generators for endpoint, service, configuration, and telemetry registration.

## Repository layout

- `src/WebApi` — HTTP application, feature handlers, database context, migrations, JSON configuration, and API startup.
- `src/WebApi.Tests` — TUnit integration tests using Testcontainers PostgreSQL.
- `src/Platform` — reusable cross-cutting setup for telemetry, OpenAPI, and exception handling.
- `src/Generators/Generators` — source generators consumed as analyzers by the application and platform projects.
- `src/GoldenPathAspnet.slnx` — solution entry point.

## Prerequisites

- Use the SDK pinned in `global.json` (currently .NET 10).
- Run commands from `src`, unless a command explicitly targets a project.
- Docker must be available to run the integration tests because they start PostgreSQL with Testcontainers.

## Common commands

```bash
dotnet restore src/GoldenPathAspnet.slnx
dotnet build src/GoldenPathAspnet.slnx --no-restore
dotnet test --solution src/GoldenPathAspnet.slnx --no-build
dotnet run --project src/WebApi/WebApi.csproj
dotnet tool restore --tool-manifest src/.config/dotnet-tools.json
```

Run `dotnet format` only when intentionally applying repository-wide formatting; avoid unrelated formatting churn.

## Implementation conventions

- Keep nullable reference types enabled and resolve all compiler warnings: `WebApi` treats warnings as errors.
- Implement API behavior as feature classes in `src/WebApi/Features`. Follow the existing `IFeature` shape: request/response DTOs, command/result mapping, validation, and a handler.
- Add `[Endpoint]` to endpoint features and `[Service]` to generated service registrations. Do not manually duplicate registrations handled by source generation.
- Add validation in the feature's nested FluentValidation validator and cover invalid input in integration tests.
- Use `TodoContext` and EF Core migrations for schema changes. Do not edit generated migration designer or model-snapshot files unless the EF tooling produces the change.
- Use the generated application-configuration APIs rather than ad-hoc configuration binding where a generated option is available.
- Preserve native-AOT compatibility: avoid reflection-based registration or serialization unless explicitly configured for it.

## Testing approach

- Treat the running API as the preferred test unit: use the startup fixture to run it in memory, backed by real PostgreSQL through Testcontainers, and exercise it through its public HTTP interface. The unit should be as large and black-box as practical.
- Add or update a test in `src/WebApi.Tests` for user-visible behavior, including invalid input.
- Use focused unit tests only for isolated logic that is genuinely simpler and more valuable to test directly. Do not create tests that mock many dependencies or require intricate setup; cover that behavior through the black-box fixture instead.
- Verify the affected project at minimum; run the solution test suite for changes to shared infrastructure, generators, database behavior, or public API contracts. When changing a source generator, build both it and its consumer projects.

## Runtime diagnostics

- For runtime failures, inspect telemetry before changing code. The Compose environment exports application logs, traces, and spans to the Aspire Dashboard at `http://localhost:18888`.
- The Aspire CLI (`aspire`) reads this telemetry directly from the terminal, without opening the dashboard UI — [install it](https://aka.ms/aspire/install-cli) if the `aspire` command is not available.
- Use `aspire otel logs --dashboard-url http://localhost:18888`, `aspire otel traces --dashboard-url http://localhost:18888`, and `aspire otel spans --dashboard-url http://localhost:18888` to view structured logs, traces, and spans respectively. Add `--non-interactive` when running from an agent/script context to avoid interactive prompts.
- Filter with options such as `--has-error` (traces/spans with errors) and `--trace-id <trace-id>` (logs/spans for a specific trace) to narrow results, e.g. `aspire otel traces --dashboard-url http://localhost:18888 --has-error --non-interactive` and `aspire otel logs --dashboard-url http://localhost:18888 --trace-id <trace-id> --non-interactive`. A trace ID returned in a problem response can therefore be used to retrieve the corresponding exception details.
- Use `aspire logs <resource>` when the application is managed by an Aspire AppHost; otherwise use the `aspire otel` commands above for this repository's Compose-based dashboard.
- Use `aspire export --dashboard-url http://localhost:18888` to save a zip snapshot of the current telemetry for offline inspection or sharing.

## Change hygiene

- Keep changes scoped to the requested behavior.
- Do not modify generated files by hand.
- Do not commit secrets or local connection strings.
- Before handing off, report the commands run and any checks that could not run (for example, Docker/Testcontainers unavailable).
