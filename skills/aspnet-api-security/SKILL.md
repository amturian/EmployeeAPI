---
name: aspnet-api-security
description: Design, explain, or review authentication and authorization for ASP.NET Core APIs, including JWT bearer validation, claims, roles, policies, and resource-level access checks.
---

# ASP.NET Core API security

Help secure ASP.NET Core APIs by distinguishing authentication (who the caller is) from authorization (what the caller may access). Inspect the target framework, existing identity provider, client type, routes, and authorization rules before suggesting an implementation.

For this repository, use the [ASP.NET Core API cheat sheet](../../Notes/ASP.NET%20Core%20API%20cheat%20sheet.md), especially its security section. Treat the course's `4-Z-final` token-minting endpoint as a deliberately unsafe demonstration, not reusable implementation code.

## Guidance

- Prefer a trusted identity provider and standard OAuth 2.0/OpenID Connect flows. Do not create a token endpoint that accepts caller-chosen usernames, roles, or permissions.
- For JWT bearer authentication, validate signature, issuer, audience, and lifetime. Keep signing keys out of source control and example defaults; plan key rotation and token lifetime/revocation behavior.
- Remember that a signed JWT payload is readable unless separately encrypted; never put secrets in claims.
- Register authentication and authorization services and ensure the pipeline authenticates before authorizing. Use `[Authorize]` or endpoint policies intentionally, and mark public endpoints explicitly.
- Distinguish `401` (authentication missing/invalid) from `403` (authenticated but not permitted) and verify the configured behavior in integration tests.
- Use claims for identity facts, roles for broad groups, and authorization policies/handlers for business rules. Never trust roles or permissions supplied by the request body or an untrusted issuer.
- Check access to the specific resource after loading it. An authenticated caller may still lack access to an employee, tenant, or child resource identified by a route ID.
- Choose browser cookie/session or bearer-token flows based on the client and deployment. For cookies consider CSRF defenses; avoid storing long-lived bearer credentials in browser-accessible storage.
- Treat API keys as limited machine-integration credentials: scope, store, rotate, and revoke them. They do not establish an interactive user's identity.
- Test anonymous, authenticated, forbidden, invalid/expired token, and cross-user/cross-tenant resource cases. Do not overstate endpoint protection based only on the presence of an `[Authorize]` attribute.
- When documenting a teaching example that is intentionally insecure, state exactly which properties make it unsafe and keep it clearly separated from production guidance.
