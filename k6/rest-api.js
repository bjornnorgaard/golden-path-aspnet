// Exercises every OpenAPI (REST) todo endpoint under load.
//
//   k6 run k6/rest-api.js
//   BASE_URL=http://localhost:8080 k6 run k6/rest-api.js
//
// Each iteration drives the full lifecycle of a todo (create -> get-by-id ->
// get-list -> update -> toggle -> delete) plus a couple of validation-error
// requests, so every endpoint and both success/failure paths get exercised.
import http from 'k6/http';
import { check, group } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE_URL, jsonHeaders, randomTitle, futureDueBy, saveSummary } from './lib/helpers.js';

// The validation-error group intentionally triggers 400/404 responses, so
// treat those as "good" for http_req_failed purposes; check() still verifies
// the exact expected status per request.
http.setResponseCallback(http.expectedStatuses(200, 400, 404));

export const options = {
  scenarios: {
    rest_api: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '10s', target: 30 },
        { duration: '20s', target: 75 },
        { duration: '10s', target: 0 },
      ],
      gracefulRampDown: '5s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800'],
  },
};

const lifecycleDuration = new Trend('rest_lifecycle_duration', true);

export default function () {
  const started = Date.now();

  group('create todo', () => {
    const payload = JSON.stringify({ title: randomTitle('rest-todo'), dueBy: futureDueBy(30) });
    const res = http.post(`${BASE_URL}/todos/create`, payload, jsonHeaders());
    check(res, {
      'create: status 200': (r) => r.status === 200,
      'create: has id': (r) => !!r.json('id'),
    });

    const id = res.json('id');
    if (!id) {
      return;
    }

    group('get todo by id', () => {
      const getRes = http.post(`${BASE_URL}/todos/get-by-id`, JSON.stringify({ id }), jsonHeaders());
      check(getRes, { 'get-by-id: status 200': (r) => r.status === 200 });
    });

    group('list todos', () => {
      const listRes = http.post(`${BASE_URL}/todos/get-list`, JSON.stringify({ limit: 20, offset: 0 }), jsonHeaders());
      check(listRes, { 'get-list: status 200': (r) => r.status === 200 });
    });

    group('update todo', () => {
      const updateRes = http.post(
        `${BASE_URL}/todos/update`,
        JSON.stringify({ id, title: randomTitle('rest-todo-updated'), dueBy: futureDueBy(60), isComplete: false }),
        jsonHeaders()
      );
      check(updateRes, { 'update: status 200': (r) => r.status === 200 });
    });

    group('toggle todo', () => {
      const toggleRes = http.post(`${BASE_URL}/todos/toggle`, JSON.stringify({ id }), jsonHeaders());
      check(toggleRes, { 'toggle: status 200': (r) => r.status === 200 });
    });

    group('delete todo', () => {
      const deleteRes = http.post(`${BASE_URL}/todos/delete`, JSON.stringify({ id }), jsonHeaders());
      check(deleteRes, { 'delete: status 200': (r) => r.status === 200 });
    });
  });

  group('validation errors', () => {
    const badCreate = http.post(`${BASE_URL}/todos/create`, JSON.stringify({ title: 'ab' }), jsonHeaders());
    check(badCreate, { 'create: invalid title -> 400': (r) => r.status === 400 });

    const badGet = http.post(`${BASE_URL}/todos/get-by-id`, JSON.stringify({ id: '00000000-0000-0000-0000-000000000000' }), jsonHeaders());
    check(badGet, { 'get-by-id: unknown id -> 404': (r) => r.status === 404 });

    const badList = http.post(`${BASE_URL}/todos/get-list`, JSON.stringify({ limit: 0, offset: 0 }), jsonHeaders());
    check(badList, { 'get-list: invalid limit -> 400': (r) => r.status === 400 });
  });

  lifecycleDuration.add(Date.now() - started);
}

export function handleSummary(data) {
  return saveSummary('rest-api', data);
}
