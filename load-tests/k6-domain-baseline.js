import http from 'k6/http';
import { check, sleep } from 'k6';
import { acquireToken } from './auth-helper.js';

// =========================================================================
// SPRINT P2 — DOMAIN CAPACITY BENCHMARK: BASELINE
// Target: Authenticated Vehicle Registration (POST /api/vehicles)
// Exercises: Kestrel -> JWT Auth -> Domain Entity -> PostgreSQL ACID ->
//            Outbox Message Table -> SKIP LOCKED Polling -> RabbitMQ
// =========================================================================

export const options = {
    vus: 5,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<400', 'p(99)<800'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    return { token: acquireToken('manager@fleetops.local', 'test1234') };
}

export default function (data) {
    const vu = __VU;
    const iter = __ITER;
    const rand = Math.floor(Math.random() * 10);
    // Unique license plate: e.g. B0100427 (length 8, alphanumeric, valid)
    const plate = `B${String(vu).padStart(2, '0')}${String(iter % 10000).padStart(4, '0')}${rand}`;

    const payload = JSON.stringify({
        licensePlate: plate,
        type: 'Van',
        make: 'Ford',
        model: 'Transit',
        year: 2024,
        mileage: 1000,
        capacityKg: 3500.0
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

    sleep(0.05);
}
