# ADR-008 — Resource Server JWT Authentication with Startup Fail-Fast Validation and Environment-Guarded Issuance

## Status
Accepted

## Context
FleetOps API exposes REST endpoints for fleet operations. Access must be restricted based on caller identity and assigned roles (`Admin`, `FleetManager`, `Dispatcher`, `Driver`).

We evaluated the authentication architecture: whether FleetOps should integrate directly with a centralized external Identity Provider (OIDC/OAuth2) or operate as a standalone Resource Server using symmetric JSON Web Tokens (JWT).

## Problem
Two opposing risks exist in token authentication design:
1. **Security Vulnerability via Insecure Defaults:** If an API defaults to hardcoded symmetric keys in source code or configuration files, attackers can forge administrative tokens (`{"role": "Admin"}`) and completely bypass authentication.
2. **Operational Over-Engineering:** Requiring a full-fledged external Identity Provider (e.g., Keycloak, Auth0, Okta, Azure AD) during initial development and automated integration testing introduces heavy infrastructure requirements, slowing down CI pipelines, onboarding, and local developer environments.
3. **Public Token Endpoint Vulnerability:** If a test helper endpoint (`POST /api/auth/token`) that generates signed JWTs without password verification is exposed to production, authentication is fundamentally compromised.

## Decision
We implement a **Hardened Resource Server Architecture** using symmetric JWT Bearer authentication (HMAC-SHA256):
1. **No Insecure Fallback & Startup Fail-Fast:**
   In `AuthenticationExtensions.cs`, the API strictly validates that `Jwt:SecretKey` is explicitly configured and meets a minimum length threshold:
   ```csharp
   if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
   {
       throw new InvalidOperationException("Fatal: 'Jwt:SecretKey' must be explicitly configured with at least 256 bits (32 characters).");
   }
   ```
   If the secret key is missing, empty, or shorter than 32 characters, the application aborts startup immediately (`fail-fast`).
2. **Cryptographic Entropy Clarification:**
   We explicitly document that validating `Length >= 32` characters in a textual string is an operational safeguard, but does not guarantee mathematical 256-bit Shannon entropy if an operator chooses a weak string. Production deployment procedures mandate injecting 32 cryptographically secure random bytes generated via CSPRNG (e.g., `openssl rand -base64 32`) through environment secrets or a secret manager.
3. **Environment-Guarded Token Endpoint:**
   `AuthController.cs` provides a `POST /api/auth/token` endpoint strictly for local development and integration testing. Outside `Development` and `Testing` environments, the endpoint returns **HTTP 404 Not Found**, ensuring zero exposure in production.
4. **Official Microsoft JwtBearerHandler Pipeline:**
   Incoming requests are validated by ASP.NET Core's official `JwtBearerHandler`, verifying issuer, audience, lifetime, clock skew, and HMAC signature. Integration tests in `AuthApiTests.cs` validate this real cryptographic pipeline against 8 adversarial scenarios.

## Alternatives Considered
1. **Immediate Federated Identity Provider (Keycloak / Auth0):**
   - *Consideration:* Standard in large enterprise multi-service environments.
   - *Rejection for Current Scope:* Requires running a dedicated Keycloak container in Docker Compose and CI, creating Realms, Clients, and Users before running a single test. The single-service bounded context does not currently warrant this complexity.
2. **API Keys / Basic Authentication:**
   - *Rejection:* Lacks cryptographic claims, role structures, expiration lifecycles, and standard authorization policy integration.

## Why This Decision
- Zero external identity infrastructure required for running, testing, or auditing FleetOps.
- Rock-solid security guarantees: impossible to run in production with default credentials.
- 100% compatible with ASP.NET Core's standard RBAC `[Authorize(Roles = "...")]` policies.

## Trade-offs
- **Symmetric Key Sharing:** Both the token generator (in dev/test) and the resource server share the same secret key. In a multi-service architecture, asymmetric keys (RSA/ECDSA via JWKS) are preferred so resource servers only hold public verification keys.
- **Revocation:** Stateless JWTs cannot be revoked prior to expiration without maintaining an active blacklist or token introspection endpoint.

## Consequences
- Production deployments must supply `JWT_SECRET` via environment variable or secret store.
- Local developer onboarding is instantaneous (`dotnet run` in Development mode works out-of-the-box with isolated dev keys).

## Risks
- Misconfigured deployment environments where `ASPNETCORE_ENVIRONMENT` is erroneously set to `Development` in production. 
- *Mitigation:* Dockerfile defaults `ASPNETCORE_ENVIRONMENT=Production` and container runs non-root.

## Operational Impact
- Secret key rotation requires restarting the application pods with the new secret, invalidating existing active tokens.

## Future Evolution
- When FleetOps expands into a multi-service distributed ecosystem, transition to OpenID Connect (OIDC) using asymmetric RSA-256 keys via standard `.well-known/openid-configuration` discovery and JWKS endpoint.
