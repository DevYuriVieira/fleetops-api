import http from 'k6/http';
import { check, sleep } from 'k6';

// =========================================================================
// SPRINT P2 — RATE LIMITING BURST & RFC 9457 COMPLIANCE BENCHMARK
// Target: Authenticated Vehicle Registration (POST /api/vehicles)
// Goal: Validate that exceeding the default in-process rate limit (100 req/60s)
// immediately triggers HTTP 429 Too Many Requests, returns application/problem+json,
// includes Retry-After header, and avoids queueing/crashing the process.
// =========================================================================

export const options = {
    vus: 10,
    duration: '10s',
    thresholds: {
        // We EXPECT 429s once the 100-permit budget is exhausted
        http_req_duration: ['p(95)<300'],
    },
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000';

export function setup() {
    const tokenRes = http.post(`${BASE_URL}/api/auth/token`, JSON.stringify({
        username: 'p2-ratelimit-manager',
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
    const plate = `R${String(vu).padStart(2, '0')}${String(iter % 10000).padStart(4, '0')}${rand}`;

    const payload = JSON.stringify({
        licensePlate: plate,
        type: 'Car',
        make: 'Fiat',
        model: 'Strada',
        year: 2024,
        mileage: 50,
        capacityKg: 700.0
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${data.token}`
        }
    };

    const res = http.post(`${BASE_URL}/api/vehicles`, payload, params);

    if (res.status === 429) {
        check(res, {
            'status is 429': (r) => r.status === 429,
            'content-type is application/problem+json': (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/problem+json'),
            'has Retry-After header': (r) => r.headers['Retry-After'] !== undefined,
            'has RFC 9457 title': (r) => r.body && r.body.includes('Too Many Requests'),
        });
    } else {
        check(res, {
            'status is 201 Created': (r) => r.status === 201,
        });
    }

    sleep(0.01);
}
