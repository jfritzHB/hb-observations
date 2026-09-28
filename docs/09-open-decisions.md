# Open Business Decisions

These do not block the repository foundation, but they must be resolved before the affected slice is finalized.

| Priority | Decision | Default used in this design |
| --- | --- | --- |
| Resolved | Are Observations and Punch Lists one shared workflow or different statuses/permissions? | Separate Procore-familiar status sets defined in product requirements |
| Resolved | Who can close and reopen an item? | Any active project member during MVP; policy-based and fully audited |
| Resolved | Must a responsible subcontractor be selected at creation? | No separate picker; Project Trade resolves the configured responsible company |
| High | Should Spanish be saved automatically or only on request? | Explicit user action; always editable |
| High | What is the approved photo retention/deletion policy? | Not assumed; must be approved before production |
| Resolved | Unauthorized access: 404 or 403? | 401 unauthenticated; 404 when the resource is missing or the caller has no visibility of it; 403 when the resource is visible but the operation is not permitted (see `docs/07-security-operations.md`) |
| Resolved | Is Responsible Company directly editable on an item? | No; always derived from Project + Trade via `ProjectTrade.ResponsibleCompanyId` and re-resolved when the Trade is explicitly changed |
| Resolved | Will trade partners log in during MVP? | Yes; external authentication plus company/project-scoped visibility |
| Resolved | Must users create multiple complete items while the device has absolutely no cellular or Wi-Fi signal? | No. MVP requires **connection-interruption protection** only (see offline choices below); complete zero-signal operation is deferred until field testing demonstrates the need |
| Resolved | Must capture location always be a preconfigured Area? | No. An item location is a structured Area and/or a free-text Location Detail (max 120 characters); at least one is required to publish, and typed detail never creates Area master data |
| Resolved | May non-leaf Areas (building, level) be selected? | Yes; any active Area |
| Resolved | Is Administrator a global superuser? | Not yet; Administrator is a project-membership role |
| Resolved | Slice 1 modelling choices | Structured Area paths are unique per project; Trade Partner memberships are company scoped and exclusive of employee roles; Responsible Company names are visible to authorized project members, including Trade Partners; development seed data stays clearly fictional |
| Resolved | Must AI draft the description? | No. Manual description is always available; AI drafting runs only on explicit request |
| Medium | Can users choose existing gallery photos? | Yes, as camera fallback and supported input |
| Medium | Does each item support multiple initial photos? | One primary at capture; additional photos after creation |
| Medium | Required report/export format? | Deferred from MVP |
| Medium | Project/area/trade source of truth: app admin, Procore or import? | App-managed for MVP with future integration boundary |
| Low | Native app distribution required? | No; installable PWA first |

## Recommended discovery test

Run a 30-minute paper/prototype session with two superintendents. Give each a phone and five representative photos. Measure whether they naturally choose Project, Area, Trade and Type before taking the photo, whether carrying selections forward helps, and how often their real work areas have no usable signal.

## Offline choices

- **Connection interruption protection (approved for MVP):** If signal drops after the page is loaded, preserve the current draft and photo locally and allow retry/resume when service returns. The user generally opens the app and loads project data while connected. Implemented with the durable draft in Slice 2.
- **Complete no-signal operation (deferred):** A user can open the installed app with zero service, browse previously synchronized projects/areas/trades, capture many items, close the app, and later synchronize everything. This is valuable in basements and remote sites but adds conflict resolution, sync queues, storage limits and device-security work. Not promised for MVP; revisit only if field testing demonstrates the need.
