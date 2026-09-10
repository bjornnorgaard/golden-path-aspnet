// Shared helpers for the k6 scripts in this folder.
// Kept dependency-free (no external k6 modules) so scripts run offline.

// Defaults to the deployed production instance. Override with BASE_URL, e.g.
// BASE_URL=http://localhost:8080 k6 run k6/rest-api.js to go back to a local run.
export const BASE_URL = __ENV.BASE_URL || 'https://golden-path-aspnet.bybear.dk';

// Every route except /login, /logout, /access-denied, /healthz, and /readyz sits
// behind GitHub OAuth (see AuthenticationConfiguration.cs), so hitting a deployed
// environment needs an authenticated session cookie — there's no API-key/service
// path today. Extract the `.AspNetCore.Cookies` value from a browser session
// that's already logged in via GitHub, then set AUTH_COOKIE=".AspNetCore.Cookies=<value>"
// (e.g. in a gitignored k6/.env, `set -a; source k6/.env; set +a` before `k6 run`).
// Not needed for local runs against an unauthenticated dev instance.
const AUTH_COOKIE = __ENV.AUTH_COOKIE || '';

// Fail fast and loud instead of silently 401ing on every request: a missing
// AUTH_COOKIE against a non-local BASE_URL is almost always someone forgetting
// to export it into the shell (e.g. running `source k6/.env` instead of
// `set -a; source k6/.env; set +a`), not an actual test condition worth
// discovering 60 seconds later via a 100% failure rate.
const IS_LOCAL_TARGET = /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?(\/|$)/i.test(BASE_URL);
if (!IS_LOCAL_TARGET && !AUTH_COOKIE) {
  throw new Error(
    `AUTH_COOKIE is not set, but BASE_URL (${BASE_URL}) is not local — every route on this ` +
    'app is behind GitHub OAuth, so requests would just 401. Run: ' +
    '`set -a; source k6/.env; set +a` (populating AUTH_COOKIE there first if you haven\'t) ' +
    'before `k6 run`, or set BASE_URL to a local instance instead.'
  );
}

const JSON_HEADERS = {
  headers: {
    'Content-Type': 'application/json',
    ...(AUTH_COOKIE ? { Cookie: AUTH_COOKIE } : {}),
  },
};

export function jsonHeaders() {
  return JSON_HEADERS;
}

// Produces a reasonably unique title without any external uuid dependency.
export function randomTitle(prefix) {
  const rand = Math.random().toString(36).slice(2, 10);
  return `${prefix}-${__VU}-${__ITER}-${rand}`;
}

// ISO-8601 timestamp a random number of days in the future, used for dueBy.
export function futureDueBy(maxDays) {
  const ms = Date.now() + Math.floor(Math.random() * maxDays) * 24 * 60 * 60 * 1000;
  return new Date(ms).toISOString();
}

// Builds a handleSummary() result that writes the full k6 summary as both:
// - k6/results/<name>-<timestamp>.json / <name>-latest.json (raw data, for
//   scripted/jq-based diffing), and
// - k6/results/<name>-<timestamp>.md / <name>-latest.md (human-readable
//   report, easy to skim or paste into a PR).
// The "-latest" files are always overwritten so the most recent run is easy
// to find; the timestamped files build up a history for later comparison.
// Still prints the condensed summary to stdout too.
export function saveSummary(name, data) {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const json = JSON.stringify(data, null, 2);
  const markdown = markdownSummary(name, data);

  return {
    stdout: textSummary(data),
    [`k6/results/${name}-${timestamp}.json`]: json,
    [`k6/results/${name}-latest.json`]: json,
    [`k6/results/${name}-${timestamp}.md`]: markdown,
    [`k6/results/${name}-latest.md`]: markdown,
  };
}

// Minimal, dependency-free stand-in for k6's textSummary jslib helper (which
// requires network access to fetch), covering just what these scripts need:
// checks, thresholds, and the built-in http/custom metrics table.
function textSummary(data) {
  const lines = [];
  const metrics = data.metrics || {};

  lines.push('');
  lines.push('  checks..........: ' + (metrics.checks ? `${metrics.checks.values.passes} passed, ${metrics.checks.values.fails} failed` : 'n/a'));

  lines.push('');
  lines.push('  THRESHOLDS');
  for (const [metricName, metric] of Object.entries(metrics)) {
    if (!metric.thresholds) continue;
    for (const [thresholdName, result] of Object.entries(metric.thresholds)) {
      lines.push(`    ${metricName} ${thresholdName}: ${result.ok ? 'PASS' : 'FAIL'}`);
    }
  }

  lines.push('');
  lines.push('  KEY METRICS');
  for (const key of ['http_req_duration', 'http_req_failed', 'http_reqs', 'iterations']) {
    const metric = metrics[key];
    if (!metric) continue;
    const values = Object.entries(metric.values)
      .map(([statName, value]) => `${statName}=${typeof value === 'number' ? value.toFixed(2) : value}`)
      .join(' ');
    lines.push(`    ${key}: ${values}`);
  }
  lines.push('');

  return lines.join('\n');
}

// Renders the same summary data as a Markdown report: a metadata line,
// checks/thresholds pass-fail, and a table of key metrics. Kept dependency-free
// like textSummary() above.
function markdownSummary(name, data) {
  const metrics = data.metrics || {};
  const lines = [];

  lines.push(`# k6 results: ${name}`);
  lines.push('');
  lines.push(`- Ran: ${new Date().toISOString()}`);
  lines.push(`- Duration: ${(data.state?.testRunDurationMs / 1000).toFixed(1)}s`);
  if (metrics.checks) {
    lines.push(`- Checks: ${metrics.checks.values.passes} passed, ${metrics.checks.values.fails} failed`);
  }
  lines.push('');

  lines.push('## Thresholds');
  lines.push('');
  const thresholdRows = [];
  for (const [metricName, metric] of Object.entries(metrics)) {
    if (!metric.thresholds) continue;
    for (const [thresholdName, result] of Object.entries(metric.thresholds)) {
      thresholdRows.push([metricName, thresholdName, result.ok ? '✅ PASS' : '❌ FAIL']);
    }
  }
  if (thresholdRows.length > 0) {
    lines.push('| Metric | Threshold | Result |');
    lines.push('| --- | --- | --- |');
    for (const [metricName, thresholdName, result] of thresholdRows) {
      lines.push(`| ${metricName} | \`${thresholdName}\` | ${result} |`);
    }
  } else {
    lines.push('_No thresholds defined._');
  }
  lines.push('');

  lines.push('## Key metrics');
  lines.push('');
  lines.push('| Metric | avg | min | med | max | p(90) | p(95) |');
  lines.push('| --- | --- | --- | --- | --- | --- | --- |');
  const trendKeys = ['http_req_duration', 'http_req_waiting', ...Object.keys(metrics).filter((key) => key.endsWith('_duration') && key !== 'http_req_duration' && key !== 'iteration_duration')];
  for (const key of trendKeys) {
    const metric = metrics[key];
    if (!metric || metric.type !== 'trend') continue;
    const v = metric.values;
    lines.push(`| ${key} | ${v.avg.toFixed(2)}ms | ${v.min.toFixed(2)}ms | ${v.med.toFixed(2)}ms | ${v.max.toFixed(2)}ms | ${v['p(90)'].toFixed(2)}ms | ${v['p(95)'].toFixed(2)}ms |`);
  }
  lines.push('');

  lines.push('| Metric | Value |');
  lines.push('| --- | --- |');
  for (const key of ['http_req_failed', 'http_reqs', 'iterations', 'vus_max', 'data_received', 'data_sent']) {
    const metric = metrics[key];
    if (!metric) continue;
    const summary = Object.entries(metric.values)
      .map(([statName, value]) => `${statName}=${typeof value === 'number' ? value.toFixed(2) : value}`)
      .join(', ');
    lines.push(`| ${key} | ${summary} |`);
  }
  lines.push('');

  return lines.join('\n');
}
