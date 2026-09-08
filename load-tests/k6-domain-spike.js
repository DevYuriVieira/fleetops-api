import http from 'k6/http';
import { check, sleep } from 'k6';

// =========================================================================
// SPRINT P2 — DOMAIN CAPACITY BENCHMARK: SPIKE
// Target: Authenticated Vehicle Registration (POST /api/vehicles)
// Profile: 5 VUs (5s) -> Sudden Spike to 35 VUs (10s) -> Recovery 5 VUs (5s)
// Evaluates latency spikes, temporary connection queueing, and rapid recovery.
// =========================================================================

export const options = {
    stages: [
        { duration: '5s', target: 5 },
        { duration: '10s', target: 35 },
        { duration: '5s', target: 5 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.05'],
        http_req_duration: ['p(95)<800'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    const tokenRes = http.post(`${BASE_URL}/api/auth/token`, JSON.stringify({
        username: 'p2-spike-manager',
        role: 'FleetManager'
    }), {
        headers: { 'Content-Type': 'application/json' }
    });

    if (tokenRes.status !== 200) {
        throw new Error(`Failed to acquire JWT token: ${tokenRes.status} ${tokenRes.body}`);
    }

    return { token: JSON.parse(tokenRes.body).accessToken };
}

export default function (data) {
    const vu = __VU;
    const iter = __ITER;
    const rand = Math.floor(Math.random() * 10);
    // Unique license plate: e.g. K0100427
    const plate = `K${String(vu).padStart(2, '0')}${String(iter % 10000).padStart(4, '0')}${rand}`;

    const payload = JSON.stringify({
        licensePlate: plate,
        type: 'Car',
        make: 'Toyota',
        model: 'Corolla',
        year: 2024,
        mileage: 200,
        capacityKg: 500.0
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${data.token}`
        }
    };

    const res = http.post(`${BASE_URL}/api/vehicles`, payload, params);

    check(res, {
        'status is 201 Created': (r) => r.status === 201,
    });

    sleep(0.02);
}
