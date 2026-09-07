import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '5s', target: 5 },    // Normal load
        { duration: '10s', target: 40 },  // Sudden traffic spike
        { duration: '5s', target: 5 },    // Recovery back to normal
    ],
    thresholds: {
        // Under spike, some requests may be rate-limited (HTTP 429), but system must not crash
        http_req_duration: ['p(95)<500'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export default function () {
    const res = http.get(`${BASE_URL}/health/live`);
    check(res, {
        'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
    });

    sleep(0.02);
}
