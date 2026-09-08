// Exercises every GraphQL todo query/mutation under load.
//
//   k6 run k6/graphql-api.js
//   BASE_URL=http://localhost:8080 k6 run k6/graphql-api.js
//
// Each iteration drives the full lifecycle of a todo through the generated
// GraphQL transport (createTodo -> getTodoById -> getTodoList -> updateTodo
// -> toggleTodo -> deleteTodo) plus an invalid mutation, mirroring the REST
// scenario so the two transports can be compared apples-to-apples.
import http from 'k6/http';
import { check, group } from 'k6';
import { Trend } from 'k6/metrics';
import { BASE_URL, jsonHeaders, randomTitle, futureDueBy, saveSummary } from './lib/helpers.js';

export const options = {
  scenarios: {
    graphql_api: {
      executor: 'ramping-vus',
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

const lifecycleDuration = new Trend('graphql_lifecycle_duration', true);

function graphql(query) {
  return http.post(`${BASE_URL}/graphql`, JSON.stringify({ query }), jsonHeaders());
}

function hasNoErrors(res) {
  const body = res.json();
  return body && body.errors === undefined;
}

export default function () {
  const started = Date.now();

  group('createTodo mutation', () => {
    const title = randomTitle('gql-todo');
    const dueBy = futureDueBy(30);
    const res = graphql(`
      mutation {
        createTodo(input: { title: "${title}", dueBy: "${dueBy}" }) {
          id
          title
          isComplete
        }
      }
    `);
    check(res, {
      'createTodo: status 200': (r) => r.status === 200,
      'createTodo: no errors': hasNoErrors,
    });

    const id = res.json('data.createTodo.id');
    if (!id) {
      return;
    }

    group('getTodoById query', () => {
      const getRes = graphql(`query { getTodoById(input: { id: "${id}" }) { id title isComplete } }`);
      check(getRes, {
        'getTodoById: status 200': (r) => r.status === 200,
        'getTodoById: no errors': hasNoErrors,
      });
    });

    group('getTodoList query', () => {
      const listRes = graphql('query { getTodoList(input: { limit: 20, offset: 0 }) { todos { id title isComplete } } }');
      check(listRes, {
        'getTodoList: status 200': (r) => r.status === 200,
        'getTodoList: no errors': hasNoErrors,
      });
    });

    group('updateTodo mutation', () => {
      const updatedTitle = randomTitle('gql-todo-updated');
      const updatedDueBy = futureDueBy(60);
      const updateRes = graphql(`
        mutation {
          updateTodo(input: { id: "${id}", title: "${updatedTitle}", dueBy: "${updatedDueBy}", isComplete: false }) {
            id
            title
            isComplete
          }
        }
      `);
      check(updateRes, {
        'updateTodo: status 200': (r) => r.status === 200,
        'updateTodo: no errors': hasNoErrors,
      });
    });

    group('toggleTodo mutation', () => {
      const toggleRes = graphql(`mutation { toggleTodo(input: { id: "${id}" }) { id isComplete } }`);
      check(toggleRes, {
        'toggleTodo: status 200': (r) => r.status === 200,
        'toggleTodo: no errors': hasNoErrors,
      });
    });

    group('deleteTodo mutation', () => {
      const deleteRes = graphql(`mutation { deleteTodo(input: { id: "${id}" }) { id } }`);
      check(deleteRes, {
        'deleteTodo: status 200': (r) => r.status === 200,
        'deleteTodo: no errors': hasNoErrors,
      });
    });
  });

  group('validation error', () => {
    const badCreate = graphql('mutation { createTodo(input: { title: "ab" }) { id } }');
    check(badCreate, {
      'createTodo: invalid title status 200': (r) => r.status === 200,
      'createTodo: invalid title has errors': (r) => {
        const body = r.json();
        return body && body.errors !== undefined;
      },
    });
  });

  lifecycleDuration.add(Date.now() - started);
}

export function handleSummary(data) {
  return saveSummary('graphql-api', data);
}
