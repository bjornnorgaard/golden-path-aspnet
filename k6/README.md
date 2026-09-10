# k6 load tests

Baseline k6 load tests for the WebApi so future changes can be compared against a
known-good performance profile. These are not correctness tests (see
`src/WebApi.Tests` for that) — they exist purely to catch regressions in latency
and throughput.

## Scripts

- `rest-api.js` — drives the full OpenAPI/REST todo lifecycle (create, get-by-id,
  get-list, update, toggle, delete) plus a few validation-error requests.
- `graphql-api.js` — drives the same lifecycle through the generated GraphQL
  transport (`createTodo`, `getTodoById`, `getTodoList`, `updateTodo`,
  `toggleTodo`, `deleteTodo`) plus an invalid mutation.
- `all.js` — runs both scenarios concurrently against the same target, so the
  REST and GraphQL transports (and the shared handlers/database/telemetry
  behind them) are stressed together.
- `lib/helpers.js` — shared, dependency-free helpers (base URL, JSON headers,
  random title/date generators, summary saving).
- `results/` — Markdown and JSON summaries written by each run (git-ignored);
  see below.

Each script steps up virtual users by doubling every 10 seconds — 1, 2, 4, 8,
16 (32 VUs combined in `all.js`, since REST and GraphQL scenarios run
concurrently) — then winds down to 0 over the final 10 seconds, 60 seconds
total. The stepped, doubling ramp makes each load level visible as a distinct
plateau in graphs (e.g. Grafana/production dashboards), rather than a smooth
ramp.

## Prerequisites

- [k6](https://k6.io) installed locally (`k6 version` to check).
- The WebApi running and reachable, e.g. via `docker compose up` from the repo
  root, or `dotnet run --project src/WebApi/WebApi.csproj`.

## Running

```bash
# Defaults to http://localhost:8080 (the compose.yaml port mapping)
# Run these from the repo root so results land in k6/results/.
k6 run k6/rest-api.js
k6 run k6/graphql-api.js
k6 run k6/all.js

# Point at a different host/port
BASE_URL=http://localhost:5000 k6 run k6/rest-api.js
```

## Results and regression comparison

Every script's `handleSummary()` writes the full k6 end-of-test summary to
this folder, in both a machine-readable and a human-readable form, in
addition to printing a condensed summary to stdout:

- `results/<script>-<timestamp>.json` — full raw summary data, one file per
  run, kept for history. Best for scripted/`jq`-based comparisons.
- `results/<script>-<timestamp>.md` — a readable report (checks, thresholds,
  key metrics as tables) for the same run. Good for skimming or pasting into
  a PR description.
- `results/<script>-latest.json` / `results/<script>-latest.md` — overwritten
  every run, always the most recent result for that script.

`results/` is git-ignored (only `.gitignore`/`.gitkeep` are tracked), so
results stay local unless you deliberately archive or commit them elsewhere
(e.g. attach to a PR, or copy the `*-latest.*` files somewhere durable before
a change and diff against a fresh run after).

To check for a regression:

```bash
# Before your change
k6 run k6/rest-api.js
cp k6/results/rest-api-latest.json /tmp/rest-api-before.json
cp k6/results/rest-api-latest.md /tmp/rest-api-before.md

# After your change
k6 run k6/rest-api.js

# Quick look: compare the Markdown reports side by side
diff /tmp/rest-api-before.md k6/results/rest-api-latest.md

# Precise comparison: compare key numbers, e.g. p95 latency and request rate
jq '.metrics.http_req_duration.values, .metrics.http_reqs.values' /tmp/rest-api-before.json
jq '.metrics.http_req_duration.values, .metrics.http_reqs.values' k6/results/rest-api-latest.json
```

## Interpreting results

k6 prints `http_req_duration` percentiles and `http_req_failed` rate at the end
of the run; both scripts also enforce thresholds (`p(95)<500ms`,
`<1%` failure rate) so a regression fails the run with a non-zero exit code.
Re-run the same script before and after a change and compare the summary
output, or the saved JSON files described above, to see whether a change made
things slower.
