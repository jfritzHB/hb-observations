# Open Business Decisions

These do not block the repository foundation, but they must be resolved before the affected slice is finalized.

| Priority | Decision | Default used in this design |
| --- | --- | --- |
| Resolved | Are Observations and Punch Lists one shared workflow or different statuses/permissions? | Separate Procore-familiar status sets defined in product requirements |
| Resolved | Who can close and reopen an item? | Any active project member during MVP; policy-based and fully audited |
| Resolved | Must a responsible subcontractor be selected at creation? | No separate picker; Project Trade resolves the configured responsible company |
| High | Should Spanish be saved automatically or only on request? | Explicit user action; always editable |
| High | What is the approved photo retention/deletion policy? | Not assumed; must be approved before production |
| Resolved | Will trade partners log in during MVP? | Yes; external authentication plus company/project-scoped visibility |
| High | Must users create multiple complete items while the device has absolutely no cellular or Wi-Fi signal? | Awaiting decision; see offline choices below |
| Medium | Can users choose existing gallery photos? | Yes, as camera fallback and supported input |
| Medium | Does each item support multiple initial photos? | One primary at capture; additional photos after creation |
| Medium | Required report/export format? | Deferred from MVP |
| Medium | Project/area/trade source of truth: app admin, Procore or import? | App-managed for MVP with future integration boundary |
| Low | Native app distribution required? | No; installable PWA first |

## Recommended discovery test

Run a 30-minute paper/prototype session with two superintendents. Give each a phone and five representative photos. Measure whether they naturally choose Project, Area, Trade and Type before taking the photo, whether carrying selections forward helps, and how often their real work areas have no usable signal.

## Offline choices

- **Connection interruption protection:** If signal drops after the page is loaded, preserve the current photo and form locally and upload it when service returns. The user generally opens the app and loads project data while connected. This is the current recommended MVP baseline.
- **Complete no-signal operation:** A user can open the installed app with zero service, browse previously synchronized projects/areas/trades, capture many items, close the app, and later synchronize everything. This is valuable in basements and remote sites but adds conflict resolution, sync queues, storage limits and device-security work.
