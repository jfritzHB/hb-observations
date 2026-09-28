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
- **Area (structured):** A selectable project location from project master data. MVP supports a hierarchy such as Building > Floor > Room/Zone, and the capture screen shows the flattened path. Any active Area may be selected, including non-leaf nodes such as `Building A` or `Building A / Level 2`.
- **Location Detail:** Free-text, item-specific location context entered during capture, such as `Unit 214`, `Room 103`, `East Corridor`, `North wall` or `Above entry door` (maximum 120 characters). It may accompany a structured Area or stand alone when no suitable Area exists. Typing a Location Detail never creates or changes Area master data.
- **Item location:** A structured Area, a Location Detail, or both. An item cannot be published without at least one of them.
- **Trade:** A controlled project-specific list such as Drywall, Painting, Electrical, Plumbing or Flooring.
- **Responsible company:** Derived from the selected project Trade configuration. The field user does not separately select a subcontractor. Project administration must map each selectable trade to its responsible company for that project.

## Primary capture flow

1. User selects a project or resumes the last project.
2. User taps **New Item**.
3. User identifies the location (search/select a structured Area and/or type a Location Detail), taps a Trade, and taps **Observation** or **Punch List**.
4. User captures a photo or chooses one from the device.
5. The app stores a local draft immediately and begins upload.
6. After upload the user describes the item in English by either path:
   - **Manual:** type their own description immediately. AI is never required to save an item.
   - **AI assisted:** explicitly tap **Generate description with AI**. The server drafts an editable title and description from the photo and the capture context, labelled **AI suggestion**.
7. User may edit the English text and explicitly tap **Translate to Spanish**, whether the English was typed, generated, or generated and then edited.
8. Translation is written to a separate editable Spanish field. The English field is not overwritten.
9. User saves. The app confirms server persistence and offers **Capture Another**, carrying forward Project, Area, Location Detail (where appropriate), Trade and Item Type, each individually changeable.
10. The selected Trade automatically associates the configured responsible trade partner company.

The field mental model is: walk into a location, identify it, tap the trade, tap Observation or Punch List, take the photo. Capture must feel like a field tool, not an administrative form.

## Required behaviors

### Item creation

- Location (a structured Area and/or a Location Detail), Trade, Item Type, English description and at least one photo are required for final save.
- AI is optional. Manual description entry must always work.
- User can retake/remove a photo before final save.
- MVP supports one primary photo plus additional photos after creation.
- The server allocates an immutable project-scoped item number.

### Connectivity (resolved for MVP)

MVP provides **connection-interruption protection**: if the app was loaded while connected and connectivity drops during capture, the current draft and photo are preserved on the device and can be retried/resumed when connectivity returns (Slice 2). MVP does **not** promise complete zero-signal operation (launching with no connectivity, browsing synchronized reference data offline, capturing a whole inspection offline, a multi-item sync queue, or offline conflict resolution). That is deferred until field testing demonstrates the need.

### AI review

- AI drafting happens only on explicit request (**Generate description with AI**); manual entry is always available first.
- AI writes from the perspective of a construction superintendent documenting the selected Item Type (see `docs/06-ai-media.md`).
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
- Unauthorized project IDs return 404 or 403 according to the resource-concealment policy in `docs/07-security-operations.md` without leaking project data.
- AI and translation calls are auditable without storing hidden model reasoning.
- Application passes automated accessibility checks for labels, focus, contrast and keyboard navigation.
