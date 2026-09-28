# Product Requirements

## Purpose

The application gives field superintendents a fast, consistent way to document construction observations and punch-list deficiencies. It reduces typing by drafting a factual description from a photo and supports bilingual communication through an optional Spanish translation.

## MVP success measures

- Median time from tapping **New Item** to a saved record is 30 seconds or less in field testing.
- A user can complete the normal capture path with one hand and no keyboard use before AI review.
- No captured photo is lost when AI analysis fails, the browser refreshes, or connectivity temporarily drops.
- Every saved item has project, area, trade, item type, English description, creator, timestamp and at least one photo.
- Users can distinguish local draft, uploading, analyzing, saved and failed states.

## Roles

| Role | MVP permissions |
| --- | --- |
| Superintendent | View assigned projects; create/edit items; request AI analysis/translation; change open item status; add photos/comments |
| Project Manager | All superintendent actions; manage project areas/trades; assign responsible company; close/reopen items; export reports |
| Administrator | Manage projects, users, memberships and reference data; all PM permissions |
| Trade Partner | Log in; view project items visible to their company/trade; comment; add completion photos; change status; close or reopen during MVP |

## Core concepts

- **Observation:** A documented condition, concern or follow-up item. It may not represent defective work and does not automatically assign contractual responsibility.
- **Punch List:** Work identified as incomplete or nonconforming and expected to be corrected before closeout.
- **Area:** A selectable project location. MVP supports a hierarchy such as Building > Floor > Room/Zone, but the capture screen shows a flattened path.
- **Trade:** A controlled project-specific list such as Drywall, Painting, Electrical, Plumbing or Flooring.
- **Responsible company:** Derived from the selected project Trade configuration. The field user does not separately select a subcontractor. Project administration must map each selectable trade to its responsible company for that project.

## Primary capture flow

1. User selects a project or resumes the last project.
2. User taps **New Item**.
3. User selects Area, Trade, and Item Type.
4. User captures a photo or chooses one from the device.
5. The app stores a local draft immediately and begins upload.
6. After upload, the server requests an AI description using the chosen Area, Trade and Item Type as context.
7. The app presents an editable English title and description with an **AI suggestion** label.
8. User may edit English text and tap **Translate to Spanish**.
9. Translation is written to a separate editable Spanish field. The English field is not overwritten.
10. User saves. The app confirms server persistence and offers **Capture Another** while retaining Area, Trade and Item Type.
11. The selected Trade automatically associates the configured responsible trade partner company.

## Required behaviors

### Item creation

- Area, Trade, Item Type, English description and at least one photo are required for final save.
- AI is optional. Manual description entry must always work.
- User can retake/remove a photo before final save.
- MVP supports one primary photo plus additional photos after creation.
- The server allocates an immutable project-scoped item number.

### AI review

- AI returns a short title and factual English description.
- The user-selected Trade remains authoritative. AI may flag a possible mismatch but may not silently replace it.
- The UI must identify AI-generated text until the user accepts or edits it.
- AI failure yields Retry and Enter Manually actions.

### Translation

- Translation occurs on explicit user action after the English text is available.
- Each translation records the source English text/version.
- Editing English after translation marks Spanish as **translation may be outdated**.
- User can regenerate or manually edit Spanish.

### Workflow

Use separate, Procore-familiar status sets rather than forcing both item types through one generic enum:

- **Observation:** `Initiated`, `ReadyForReview`, `NotAccepted`, `Closed`.
- **Punch List:** `Open`, `WorkRequired`, `ReadyForReview`, `NotAccepted`, `Closed`.

For MVP simplicity, any authenticated user with access to the project may close or reopen an item, including a Trade Partner. Reopening a closed Observation returns it to `Initiated`; reopening a closed Punch List item returns it to `Open`. Every transition records actor, timestamp, old status, new status and optional note. Authorization must remain policy-based so closing permissions can be tightened after the pilot.

## MVP exclusions

- Automatic code-compliance determinations
- Automatic assignment of contractual responsibility
- Direct Procore synchronization
- Push notifications
- Unrestricted subcontractor portal; MVP trade partners receive only project/company-scoped item access
- QR-code issue markers
- PDF report distribution automation
- Conflict-free multi-device offline editing
- Native App Store/Play Store distribution

## Global acceptance criteria

- Works at 360px phone width through common tablet sizes.
- Capture controls are usable in portrait orientation and direct sunlight with high contrast.
- Refreshing during upload/review restores the draft.
- Duplicate Save taps create one item, using an idempotency key.
- Unauthorized project IDs return 404 or 403 according to the established security policy without leaking project data.
- AI and translation calls are auditable without storing hidden model reasoning.
- Application passes automated accessibility checks for labels, focus, contrast and keyboard navigation.
