# Claude Code Instructions

Read every file in `docs/` before making architectural changes. Treat the documents as the product contract. If code and documentation disagree, stop and report the conflict.

## Working rules

- Implement one vertical slice at a time in the order defined in `docs/08-implementation-plan.md`.
- Before coding a slice, inspect the repository and write a short plan naming files to change.
- Keep controllers thin. Put use cases in the application layer and invariants in the domain layer.
- Use nullable reference types, async I/O and cancellation tokens.
- Store all timestamps as UTC `DateTimeOffset`; render in the project/user time zone.
- Use `Guid` identifiers externally. Generate human-readable item numbers server-side per project.
- Never expose storage account keys, OpenAI keys or unrestricted blob URLs to the browser.
- Do not allow the AI response to directly save or close an item. It only proposes editable values.
- Do not infer safety, code compliance, concealed conditions or certainty from a photo.
- Preserve the user's English text when translating. Translation writes only to the Spanish field.
- Every write endpoint must enforce authorization, validate input and create an audit event.
- In the MVP, any authenticated member of the item's project, including a Trade Partner, may close or reopen an item. Preserve this permissive rule behind an authorization policy so it can be tightened later without rewriting workflow logic.
- Resolve the responsible company from the selected Project Trade configuration. Do not add a required subcontractor picker to field capture.
- Use optimistic concurrency (`rowversion`/ETag) for edits and state changes.
- Add or update tests with every behavior change. Do not weaken tests to make a build pass.
- Run formatting, linting, unit tests and integration tests before reporting completion.
- Never commit secrets, generated credentials, production URLs or real construction photos.
- Keep the API container stateless, listen on port 8080, expose `/health/live` and `/health/ready`, and verify the production Docker build whenever project structure changes.
- The API must serve the compiled React application from `wwwroot` with SPA fallback, unless the repository explicitly adopts separately hosted static assets.

## Required initial output

Before implementation, produce:

1. Repository audit.
2. Proposed solution/project structure.
3. Slice being implemented and excluded scope.
4. Risks or blocking decisions from `docs/09-open-decisions.md`.

## UX constraints

- Phone portrait is the primary viewport; tablet landscape is secondary.
- Primary touch targets are at least 44 by 44 CSS pixels.
- The capture path must not require typing before the photo (Browse areas and recent chips give a no-typing path).
- Capture location is a structured Area and/or a free-text Location Detail (max 120 characters); publishing requires at least one. Typed text is always Location Detail: a structured Area is attached only when the user explicitly taps a suggestion (never by automatic matching), and typed text never creates Area master data.
- The authoritative capture context is Project, structured Area (when selected), Location Detail (when present), Trade and Item Type. Capture Another retains all of it after save, each value individually changeable, so the user can rapidly capture another item; allow one-tap clearing.
- Area suggestions stay project-wide even after an Area is selected; explicitly selecting another suggestion replaces the selected Area. Never alter Location Detail except through an explicit user action.
- Show upload and AI analysis as separate progress states.
- A failed AI call must never discard the photo or prevent manual entry and save.
- Never label an unsynchronized local draft as saved to the server.

## Completion report

Report changed files, migrations, tests run and results, manual test steps, known limitations, and the next recommended slice. Do not claim a behavior was verified unless it was actually tested.
