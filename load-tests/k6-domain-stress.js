import http from 'k6/http';
import { check, sleep } from 'k6';

// =========================================================================
// SPRINT P2 — DOMAIN CAPACITY BENCHMARK: STRESS & SATURATION
// Target: Authenticated Vehicle Registration (POST /api/vehicles)
// Profile: Progressive ramp: 5 -> 15 -> 30 -> 50 -> 70 VUs
// Goal: Identify the latency knee, Npgsql connection pool saturation,
// and degradation inflection point under real database transactions.
// =========================================================================

export const options = {
    stages: [
        { duration: '5s', target: 5 },    // Warm-up
        { duration: '10s', target: 15 },  // Moderate load
        { duration: '10s', target: 30 },  // High load
        { duration: '10s', target: 50 },  // Heavy saturation testing
        { duration: '10s', target: 70 },  // Peak stress
        { duration: '5s', target: 0 },    // Cool-down
    ],
    thresholds: {
        // Under extreme stress, latency may elevate but we want to observe the exact threshold
        http_req_duration: ['p(95)<3000'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    const tokenRes = http.post(`${BASE_URL}/api/auth/token`, JSON.stringify({
        username: 'p2-stress-manager',
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
    // Unique license plate: e.g. X0100427
    const plate = `X${String(vu).padStart(2, '0')}${String(iter % 10000).padStart(4, '0')}${rand}`;

    const payload = JSON.stringify({
        licensePlate: plate,
        type: 'Van',
        make: 'Mercedes',
        model: 'Sprinter',
        year: 2024,
        mileage: 1500,
        capacityKg: 3200.0
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${data.token}`
        }
    };

    const res = http.post(`${BASE_URL}/api/vehicles`, payload, params);

    check(res, {
        'status is 201 Created or 429': (r) => r.status === 201 || r.status === 429,
    });

    sleep(0.02);
}
