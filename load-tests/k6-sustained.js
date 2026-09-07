import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 15,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.05'],
        http_req_duration: ['p(95)<300', 'p(99)<600'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    // Acquire JWT token for write operations
    const tokenRes = http.post(`${BASE_URL}/api/auth/token`, JSON.stringify({
        username: 'loadtest-manager',
        role: 'FleetManager'
    }), {
        headers: { 'Content-Type': 'application/json' }
    });

    if (tokenRes.status === 200) {
        return { token: JSON.parse(tokenRes.body).accessToken };
    }
    return { token: null };
}

export default function (data) {
    const params = {
        headers: {
            'Content-Type': 'application/json',
        }
    };

    if (data.token) {
        params.headers['Authorization'] = `Bearer ${data.token}`;
    }

    // Health read probe
    const resLive = http.get(`${BASE_URL}/health/live`, params);
    check(resLive, {
        'live returns 200': (r) => r.status === 200,
    });

    const resReady = http.get(`${BASE_URL}/health/ready`, params);
    check(resReady, {
        'ready returns 200': (r) => r.status === 200,
    });

    sleep(0.05);
}
