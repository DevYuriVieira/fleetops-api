import http from 'k6/http';
import { check, sleep } from 'k6';

// =========================================================================
// SPRINT P2 — DOMAIN CAPACITY BENCHMARK: SUSTAINED
// Target: Authenticated Vehicle Registration (POST /api/vehicles)
// Concurrency: 15 VUs sustained for 45s
// Measures continuous database write throughput, connection pool stability,
// and outbox generation under sustained business load.
// =========================================================================

export const options = {
    vus: 15,
    duration: '45s',
    thresholds: {
        http_req_failed: ['rate<0.02'],
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    const tokenRes = http.post(`${BASE_URL}/api/auth/token`, JSON.stringify({
        username: 'p2-sustained-manager',
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
    // Unique license plate: e.g. S0100427 (length 8, alphanumeric, valid)
    const plate = `S${String(vu).padStart(2, '0')}${String(iter % 10000).padStart(4, '0')}${rand}`;

    const payload = JSON.stringify({
        licensePlate: plate,
        type: 'Truck',
        make: 'Volvo',
        model: 'FH540',
        year: 2024,
        mileage: 5000,
        capacityKg: 40000.0
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

    sleep(0.04);
}
