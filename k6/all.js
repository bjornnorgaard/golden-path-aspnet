// Runs the REST and GraphQL lifecycle scenarios concurrently against the
// same target, stressing both transports (and therefore the shared handlers,
// database, and telemetry pipeline) at once.
//
//   k6 run k6/all.js
//   BASE_URL=http://localhost:8080 k6 run k6/all.js
import runRestLifecycle from './rest-api.js';
import runGraphqlLifecycle from './graphql-api.js';
import { saveSummary } from './lib/helpers.js';

export const options = {
  scenarios: {
    rest_api: {
      executor: 'ramping-vus',
      exec: 'restApi',
      startVUs: 0,
      stages: [        
        { duration: '25s', target: 10 },        
        { duration: '5s', target: 0 },        
      ],
      gracefulRampDown: '5s',
    },
    graphql_api: {
      executor: 'ramping-vus',
      exec: 'graphqlApi',
      startVUs: 0,
      stages: [
        { duration: '25s', target: 10 },
        { duration: '5s', target: 0 },
      ],
      gracefulRampDown: '5s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800'],
  },
};

export function restApi() {
  runRestLifecycle();
}

export function graphqlApi() {
  runGraphqlLifecycle();
}

export function handleSummary(data) {
  return saveSummary('all', data);
}
