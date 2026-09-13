import http from 'k6/http';

const KEYCLOAK_URL = __ENV.KEYCLOAK_URL || 'http://localhost:8080';
const KEYCLOAK_REALM = __ENV.KEYCLOAK_REALM || 'fleetops';
const CLIENT_ID = __ENV.KEYCLOAK_CLIENT_ID || 'fleetops-api';
const CLIENT_SECRET = __ENV.KEYCLOAK_CLIENT_SECRET || 'fleetops-api-dev-secret';

export function acquireToken(username = 'manager@fleetops.local', password = 'test1234') {
    if (__ENV.AUTH_TOKEN) {
        return __ENV.AUTH_TOKEN;
    }

    const tokenUrl = `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`;
    const payload = {
        client_id: CLIENT_ID,
        client_secret: CLIENT_SECRET,
        grant_type: 'password',
        username: username,
        password: password,
    };

    const res = http.post(tokenUrl, payload);
    if (res.status !== 200) {
        throw new Error(`Failed to acquire OIDC token from Keycloak (${tokenUrl}): [${res.status}] ${res.body}`);
    }

    const json = JSON.parse(res.body);
    return json.access_token;
}

export function tryAcquireToken(username = 'manager@fleetops.local', password = 'test1234') {
    try {
        return acquireToken(username, password);
    } catch {
        return null;
    }
}
