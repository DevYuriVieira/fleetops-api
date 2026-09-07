import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 5,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'], // Less than 1% failures
        http_req_duration: ['p(95)<200', 'p(99)<350'], // 95% of requests must complete below 200ms
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export default function () {
    // 1. Health Probe
    const liveRes = http.get(`${BASE_URL}/health/live`);
    check(liveRes, {
        'live status is 200': (r) => r.status === 200,
    });

    // 2. Dependencies Probe
    const depRes = http.get(`${BASE_URL}/health/dependencies`);
    check(depRes, {
        'dependencies status is 200': (r) => r.status === 200,
        'dependencies contains status': (r) => r.body.includes('status'),
    });

    sleep(0.1);
}
