# Security and Operations

## Authentication and authorization

Use Microsoft Entra ID for employee SSO. Every request resolves the authenticated user to an application user and project membership. Policies combine global role, project membership and resource ownership/status. Never authorize solely because a user knows an item ID.

Trade-partner authentication is part of the MVP. Use the organization's approved Microsoft external identity/guest flow. An authenticated external identity has no application access until an Administrator links it to a Company and ProjectMembership. Queries must enforce both project membership and company/trade visibility. Never authorize from email domain alone.

### Resource concealment (401 / 403 / 404)

Resolved by the product owner:

- **401 Unauthorized:** the caller is not authenticated.
- **404 Not Found:** the resource does not exist, **or** the caller has no visibility of it (for example, no active membership in its project, or outside a Trade Partner's company scope) and therefore must not learn whether it exists. Both cases return the same response.
- **403 Forbidden:** the caller is authenticated and may know the resource exists (it is visible to them), but lacks permission for the requested operation.

Visibility is always evaluated before permission. For example, a Trade Partner requesting a project or item outside their memberships or company visibility receives 404; a member who can see a project but attempts an administrative operation they may not perform receives 403.

MVP permits every active project member to close or reopen items. Implement this through named authorization policies and audit every transition so the rule can be restricted later without changing stored data or API shape.

## Data protection

- TLS only; encryption at rest through managed Azure services.
- Secrets and connection strings in Key Vault or managed platform configuration.
- Prefer managed identities between Azure resources.
- Private blob containers; short-lived least-privilege upload/read grants.
- Validate file signatures in addition to MIME type and extension.
- Sanitize filenames and use generated blob keys.
- Define photo retention, export and deletion with legal/operations stakeholders.

## Audit

Audit security and business events: sign-in failures, membership/reference-data changes, item creation/edit/publish, photo changes, AI/translation requests, status transitions, exports and administrative actions. Audit records are append-only and contain actor, UTC time, project/item, action, correlation ID and minimal sanitized detail.

## Observability

Track API latency/error rate, upload success, AI queue depth and age, AI latency/failure/cost, translation failures, draft abandonment and capture completion time. Use structured logs with correlation IDs. Never log tokens, full SAS URLs, raw authorization headers or photo bytes.

## CI/CD gates

- Restore/build with warnings policy
- Frontend typecheck, lint and production build
- Unit and integration tests
- API contract/OpenAPI validation
- Dependency and secret scanning
- EF migration review/script generation
- Deployment to test, smoke tests, then approved production promotion

## Backup and recovery

Enable Azure SQL point-in-time restore and blob protection appropriate to retention requirements. Document recovery objectives before production. Test restoration, not just backup configuration.

## Environments

Local, Test and Production use separate identity registrations, databases, storage, AI deployments and secrets. Never copy production photos into lower environments. Seed synthetic projects, areas, trades and users for automated/manual testing.
