# ADR-016 — OpenID Connect / OAuth 2.0 Resource Server with Keycloak and Asymmetric RS256 Validation

## Status
Accepted (Supersedes [ADR-008](ADR-008-jwt-over-external-idp.md))

## Context
In ADR-008, FleetOps API implemented a symmetric JWT (HMAC-SHA256) authentication scheme where the API held the signing secret key (`Jwt:SecretKey`) and contained a development-only token issuance endpoint (`POST /api/auth/token`). 

While suitable for initial development, this architecture presented fundamental enterprise limitations:
1. **Secret Sharing & Blast Radius:** Sharing a symmetric key between issuers and verifiers allows anyone with access to the verification key to forge tokens arbitrarily.
2. **Co-located Token Issuance:** Hosting token issuance inside the resource server violates the separation of concerns between Identity Provider (IdP) and Resource Server (RS).
3. **Enterprise SSO & Federation Incompatibility:** Modern cloud ecosystems require standard OpenID Connect (OIDC) / OAuth 2.0 protocols, asymmetric signatures (RS256/ES256), cryptographic key rotation via JWKS, and centralized user/credential management with MFA, RBAC, and federation capabilities.

## Problem
Migrating FleetOps API to an external IdP requires addressing several architectural and security concerns:
- FleetOps must act purely as an OAuth 2.0 Resource Server without holding private keys or issuing tokens.
- Token validation must enforce cryptographic signatures, issuer canonization, audience verification, lifetime expiration, and small clock skew.
- The Identity Provider's JWKS (JSON Web Key Set) must be fetched dynamically via OIDC discovery document (`/.well-known/openid-configuration`), supporting seamless key rotation without requiring API restarts or secret re-deployments.
- Roles issued by the IdP must be deterministically mapped to ASP.NET Core `ClaimTypes.Role` without privilege escalation or ambiguous parser fallbacks.
- Local development, Docker Compose, and CI automation must remain fully functional and reproducible without manual realm bootstrapping.

## Decision
We transition FleetOps API to an **OAuth 2.0 / OpenID Connect Resource Server** backed by **Keycloak**:

### 1. Architectural Roles
- **Keycloak (Identity Provider / Authorization Server):**
  - Manages realms, users, client applications, credentials, and client scopes.
  - Generates RSA-2048 signing keys and issues RS256 JWT access tokens.
  - Exposes standard OIDC discovery (`/realms/fleetops/.well-known/openid-configuration`) and JWKS endpoint (`/realms/fleetops/protocol/openid-connect/certs`).
- **FleetOps API (Resource Server):**
  - Holds zero private keys and zero symmetric secrets.
  - Completely eliminates local token generation (`AuthController` and `TokenRequest`/`TokenResponse` removed).
  - Validates incoming Bearer tokens using ASP.NET Core native `JwtBearerHandler` (via `Microsoft.AspNetCore.Authentication.JwtBearer`).
  - Fetches public keys dynamically from Keycloak's JWKS and verifies RS256 signatures.

### 2. Strict Protocol & Cryptographic Validations
In `AuthenticationExtensions.cs`, token validation enforces:
- **Authority & Audience Startup Fail-Fast:** If `Jwt:Authority` or `Jwt:Audience` is missing or whitespace, the API immediately aborts startup with an explicit fatal exception.
- **Canonical Issuer Validation:** Token `iss` must match the canonical issuer URL exactly. In containerized environments, `Jwt:ValidIssuer` specifies the canonical host-facing issuer while `Jwt:MetadataAddress` directs backchannel discovery calls to the internal network address.
- **Explicit Audience Restriction:** `ValidateAudience = true` and `ValidAudience = "fleetops-api"`. Tokens not targeted at FleetOps API are rejected.
- **Asymmetric Signature Verification:** `ValidateIssuerSigningKey = true`, restricted strictly to `SecurityAlgorithms.RsaSha256`.
- **Lifetime & Skew Control:** `ValidateLifetime = true` with a small, explicit `ClockSkew = TimeSpan.FromMinutes(1)`.

### 3. Deterministic Role Mapping
Keycloak encapsulates client-specific roles under `resource_access.{clientId}.roles`. In FleetOps:
- The canonical role source is strictly `resource_access.fleetops-api.roles`.
- During `OnTokenValidated`, a focused JSON extractor parses `resource_access.fleetops-api.roles` and injects each role into `ClaimTypes.Role`.
- Generic, ambiguous fallbacks across multiple paths (`realm_access.roles`, root `role`, etc.) are prohibited to eliminate privilege escalation risks.
- Existing authorization policies (`[Authorize(Roles = "Admin,FleetManager")]`) continue functioning transparently.

### 4. Zero Secrets & Key Rotation (JWKS)
- Key rotation on the IdP is handled without downtime: when Keycloak activates a new signing key (`kid`), `ConfigurationManager` in `JwtBearerHandler` automatically refreshes the cached JWKS upon encountering an unknown `kid` or reaching refresh intervals.
- The API operates without storing passwords, client secrets, or private keys in source control, environment files, or logs.

### 5. Environments (Dev, Docker, CI)
- **Local Dev / Testing:** `TestOidcAuthority` provides an in-process, cryptographic RSA-2048 OIDC server for high-speed automated testing without requiring an external container.
- **Docker Compose:** Spawns `keycloak` (Quay `26.1.0`) with `--import-realm` preloading `keycloak/realm-export.json`. Service dependency healthcheck ensures Keycloak is ready on `/health/ready` before the API starts.
- **GitHub Actions CI:** Starts the Keycloak container, awaits readiness, and executes live E2E integration tests against real Keycloak tokens.

## Client Flows & Authentication Grants
1. **Interactive Users (Web/Mobile):**
   - Must use **Authorization Code Flow with PKCE** (Proof Key for Code Exchange).
2. **Machine-to-Machine (Service Integrations):**
   - Must use **OAuth 2.0 Client Credentials Grant**.
3. **Direct Access Grants (Resource Owner Password Credentials):**
   - Treated **strictly as a test automation convenience** for CI and integration tests.
   - Disabled/deprecated for interactive modern applications.
   - Synthetic test credentials (`test1234`) are isolated to non-production environments.

## Alternatives Considered
1. **Retaining Symmetric HMAC-SHA256 (ADR-008):**
   - *Rejected:* Symmetric keys cannot be shared with third parties or external services without exposing token-signing authority.
2. **Custom Identity / Membership Provider in EF Core:**
   - *Rejected:* Implementing user password hashing, MFA, account lockout, token refresh, and protocol compliance from scratch violates security best practices and distracts from core fleet logistics domain logic.
3. **Cloud-Only Identity Provider (Auth0 / AWS Cognito / Entra ID):**
   - *Deferred:* Keycloak provides an open-source, standard-compliant OIDC IdP that runs identically locally, in Docker, and in CI without cloud vendor lock-in or external API dependencies.

## Consequences
- **Positive:**
  - FleetOps API is fully decoupled from identity management, user tables, and password policies.
  - Zero private keys in the application layer.
  - Production-ready compliance with OAuth 2.0 / OIDC standards.
  - Seamless key rotation supported via JWKS discovery.
  - Clean Architecture preserved: Domain and Application layers have zero knowledge of Keycloak or OIDC.
- **Negative / Trade-offs:**
  - Local Docker Compose environment now requires running a Keycloak container (~25s startup).
  - Test suites require either a synthetic OIDC authority (used in unit/adversarial tests) or a live Keycloak container (used in E2E tests).
  - Clock synchronization between IdP and Resource Server is critical (mitigated by 1-minute ClockSkew).
