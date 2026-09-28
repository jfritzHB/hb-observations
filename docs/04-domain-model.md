# Domain Model

## Entities

### Project

`Id`, `Number`, `Name`, `TimeZoneId`, `Status`, `CreatedAt`, `RowVersion`.

### ProjectMembership

Links User to Project with one or more roles and an optional CompanyId. Authorization must require membership, not only a global role claim. Trade Partner membership is company scoped.

### Area

`Id`, `ProjectId`, `ParentAreaId?`, `Name`, `Path`, `SortOrder`, `IsActive`, `RowVersion`. Prevent cycles. Item creation requires an active area.

### Trade

`Id`, `Code`, `Name`, `IsActive`. `ProjectTrade` enables a trade for a project and contains `ResponsibleCompanyId`. The mapping is required before the trade can be selected for new field capture. Do not store trade as uncontrolled text on an item.

### Company

`Id`, `Name`, `IsActive`; optionally linked to supported trades and projects.

### FieldItem

| Field | Rule |
| --- | --- |
| `Id` | Guid |
| `ProjectId` | Required and authorized |
| `ItemNumber` | Server-generated, unique per project |
| `Type` | `Observation` or `PunchList` |
| `LifecycleState` | `Draft` or `Published` |
| `ObservationStatus?` | `Initiated`, `ReadyForReview`, `NotAccepted`, `Closed`; populated only for Observation |
| `PunchListStatus?` | `Open`, `WorkRequired`, `ReadyForReview`, `NotAccepted`, `Closed`; populated only for Punch List |
| `AreaId?`, `AreaPathSnapshot?` | Structured Area; optional when `LocationDetail` is present |
| `LocationDetail?` | Free-text, item-specific location context, max 120 characters (for example `Unit 214`, `North wall`). Never creates or modifies Area master data |
| `TradeId`, `TradeNameSnapshot` | Required |
| `ResponsibleCompanyId`, snapshot | Derived from Project Trade at creation; not separately selected and never directly editable |
| `Title` | Required to publish, max 80 |
| `DescriptionEnglish` | Required to publish, max 2,000 |
| `DescriptionSpanish?` | Optional, max 2,000 |
| `TranslationSourceHash?` | Detects stale translation |
| `Priority` | `Normal`, `High`, `Critical`; default Normal |
| `CreatedByUserId`, timestamps | Immutable audit metadata |
| `RowVersion` | Optimistic concurrency |

### FieldItemPhoto

`Id`, `FieldItemId`, `BlobKey`, `ThumbnailBlobKey`, `MediaType`, `ByteLength`, `Width`, `Height`, `Sha256`, `SortOrder`, `CapturedAt?`, `UploadedAt`, `UploadedByUserId`, `IsPrimary`. Blob keys are stored, not public URLs.

### AiAnalysis

Stores job status, provider/model deployment, prompt template version, input photo IDs, user context, structured output, error category, token/cost telemetry where available, and timestamps. Do not store chain-of-thought.

### TranslationRevision

Stores source language, target language, source-text hash, translated text, model deployment, prompt version, creator and timestamp. The current Spanish field can point to the accepted revision.

### FieldItemEvent

Append-only audit record: item ID, event type, actor, occurred-at, correlation ID and sanitized JSON details. Examples: Created, PhotoAdded, AiSuggested, DescriptionEdited, TranslationGenerated, StatusChanged, Reopened.

### Comment

`Id`, `FieldItemId`, `Body`, `AuthorUserId`, `CreatedAt`, `EditedAt?`. Comments are not part of the description.

## Invariants

- Published item requires at least one finalized photo.
- Published item requires meaningful location: a structured Area, a Location Detail, or both. A draft may temporarily lack location. `FieldApp.Domain.Capture.ItemLocation` (Slice 1.1) encodes this rule ahead of FieldItem.
- Any active Area (including non-leaf nodes) may be referenced by an item. Location Detail is item data only; it never creates or modifies Areas.
- Observation transitions normally follow `Initiated -> ReadyForReview -> Closed`; rejection uses `ReadyForReview -> NotAccepted`, and corrected work uses `NotAccepted -> ReadyForReview`.
- Punch List transitions normally follow `Open -> WorkRequired -> ReadyForReview -> Closed`; rejection uses `ReadyForReview -> NotAccepted`, and corrected work uses `NotAccepted -> ReadyForReview`.
- During MVP any active project member may close. Reopening is explicit, requires a reason, and returns Observations to `Initiated` and Punch List items to `Open`.
- Status permissions must be implemented as policies so the MVP rule can change without altering domain transitions.
- Trade and Area snapshots (and Location Detail) update only by an explicit item edit, never when master data changes.
- Responsible Company is resolved and snapshotted from Project Trade whenever the item's Trade is explicitly changed.
- Changing English text after translation sets `TranslationState=Stale` when its hash differs.
- Deleting a photo is soft/controlled and audited; the last photo cannot be removed from a published item without adding a replacement.

## Numbering

Use a database-backed per-project sequence, displayed as `OBS-0001` or `PCH-0001`. Do not calculate `MAX + 1`. Number allocation must be concurrency safe and may leave gaps.
