# Implementation Plan

Build vertical slices that result in demonstrable field behavior. Do not provision full production infrastructure before the capture workflow is validated.

## Slice 0 Repository foundation

- Create solution/projects and React app.
- Add formatting, linting, nullable references, central package management and test projects.
- Add local development via containers or documented dependencies.
- Add health endpoint, Problem Details, correlation IDs and OpenAPI.
- Add CI that builds/tests both stacks.
- Make the production multi-stage Docker build pass and run a container smoke test against `/health/live`.

**Done:** clean checkout runs locally with one documented command; tests and CI pass; `docker compose up --build` starts the application and database; the production container responds on port 8080.

## Slice 1 Reference data and mobile shell

- Authentication development stub behind the same user abstraction as Entra.
- Project membership authorization.
- Project, Area hierarchy, Company and Project Trade-to-responsible-company mapping entities/endpoints.
- Phone navigation and Area/Trade/Type selection UI.
- Seed synthetic demo project.

**Done:** superintendent can select only authorized project data on a 360px viewport, and Trade selection resolves a responsible company without another capture field.

## Slice 2 Durable capture and photo

- IndexedDB local draft.
- Server draft creation with idempotency.
- Camera/file picker, compression, preview, upload/finalize and thumbnail.
- Restore draft after refresh; precise upload/error states.

**Done:** capture survives refresh and failed upload; retry produces one server item and one photo.

## Slice 3 AI description

- AI provider abstraction and fake provider for tests/local use.
- Background analysis job and status API.
- Strict structured output validation and prompt versioning.
- Review/edit UI and manual fallback.

**Done:** successful, slow, invalid-output and unavailable-provider paths are tested; none loses the photo.

## Slice 4 Translation and publish

- Translation operation and source hash.
- Editable Spanish field and stale indicator.
- Publish validation and server item numbering.
- Capture Another with retained context.

**Done:** English is never overwritten; duplicate taps remain idempotent; published item is queryable.

## Slice 5 Item list, trade partner access and workflow

- Mobile filters by type/status/area/trade.
- Detail timeline, comments and additional photos.
- External Trade Partner sign-in, invitation/membership administration and company-scoped item list.
- Procore-familiar type-specific state transitions, MVP close/reopen rule and concurrency UI.

**Done:** a Trade Partner can see only permitted company/project items, respond with comments/photos, and use permitted transitions; invalid transitions fail safely and all accepted transitions are audited.

## Slice 6 Pilot hardening

- Employee and external Trade Partner Microsoft identity integration, managed identities and environment configuration.
- Accessibility, performance and device matrix testing.
- Telemetry dashboards, alerting, retention configuration and recovery runbook.
- Field usability test with representative superintendents.

**Done:** security review and pilot checklist pass; capture-time metric can be measured.

## Minimum automated tests

- Domain transition and publication invariants
- Project-scoped authorization
- Concurrent numbering and ETag conflict
- Creation/upload/finalize idempotency
- File validation and inaccessible blob behavior
- AI schema validation, timeout and retry exhaustion
- Translation staleness hashing
- API integration tests using real database/storage emulators or test containers
- Playwright happy path at phone viewport plus permission/AI/network failure paths

## Pilot exit criteria

- At least two superintendent users complete representative walks.
- Median capture time meets target or documented changes are approved.
- No critical photo-loss, authorization or duplicate-item defects.
- AI descriptions are accepted or lightly edited at an agreed rate measured during pilot.
- Operations owner approves retention, support and incident process.
