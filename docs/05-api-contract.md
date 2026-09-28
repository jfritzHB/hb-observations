# REST API Contract

Base path: `/api/v1`. Use RFC 7807 Problem Details for errors. All writes accept `X-Correlation-Id`; item creation and retried operations accept `Idempotency-Key`.

## Reference data

- `GET /projects`
- `GET /projects/{projectId}`
- `GET /projects/{projectId}/areas?search=&recent=true`
- `POST /projects/{projectId}/areas` (PM/Admin)
- `GET /projects/{projectId}/trades` (includes configured responsible company display data)
- `GET /projects/{projectId}/companies?tradeId=`
- `GET /projects/{projectId}/areas/{areaId}` (target of the `Location` header returned by area creation)
- `GET /me` (the resolved application user)

The trades list contains only trades available for capture: the Project Trade is enabled, the trade is active, and it is mapped to an active responsible company. Areas returned for capture are active and have only active ancestors. `recent=true` is accepted; recency ordering needs a user's captured items, so until items exist it has no server-side effect and clients keep device-local recents. In the Development environment only, `GET /dev/personas` lists the synthetic personas used by the development authentication stub.

## Item capture

### Create server draft

`POST /projects/{projectId}/items`

```json
{
  "clientDraftId": "87f39a7f-d175-4dc8-a562-c132f9a7cf07",
  "type": "PunchList",
  "areaId": "970db077-d787-48b4-83b2-1966f6920f73",
  "tradeId": "eb28dcc4-a8f2-49c7-93c8-43a8d89e42a3",
  "priority": "Normal"
}
```

Returns `201`, draft representation, ETag and item-scoped media instructions. The server resolves `responsibleCompanyId` from the selected project Trade; clients cannot override it during capture.

### Request upload

`POST /items/{itemId}/photos/uploads`

```json
{
  "fileName": "capture.jpg",
  "contentType": "image/jpeg",
  "byteLength": 1834201,
  "sha256": "base64-sha256"
}
```

Return a narrow upload URL or API upload token, expiry, photo ID, allowed content type and maximum bytes.

### Finalize upload

`POST /items/{itemId}/photos/{photoId}/finalize`

Verifies blob existence, size/type/hash, extracts dimensions, creates thumbnail and marks photo ready. It is idempotent.

### Analyze

`POST /items/{itemId}/ai-analyses`

```json
{
  "photoIds": ["6bffb239-92d2-4ddd-bf30-e30fe1d2eb27"],
  "userNote": null
}
```

Returns `202` with analysis ID and status URL. `GET /ai-analyses/{analysisId}` returns job state and, on success:

```json
{
  "status": "Succeeded",
  "suggestion": {
    "title": "Damaged drywall at doorway",
    "descriptionEnglish": "Repair the chipped drywall at the lower right side of the doorway and prepare the surface for finish paint.",
    "possibleTradeMismatch": false,
    "uncertaintyNote": null
  }
}
```

### Update and publish

- `PATCH /items/{itemId}` with `If-Match`
- `POST /items/{itemId}/publish` with `If-Match`
- `GET /items/{itemId}`
- `GET /projects/{projectId}/items?type=&status=&areaId=&tradeId=&cursor=`

Patch uses an explicit request DTO, not arbitrary JSON Patch. Editable fields are Area, Trade, Type, Priority, Title and descriptions. Responsible Company is not directly editable: it is derived from Project + Trade through `ProjectTrade.ResponsibleCompanyId` (the authoritative mapping). Clients supply `tradeId`, never a company ID; when an item's Trade is explicitly changed, the server resolves and snapshots the Responsible Company from the new Project Trade mapping.

## Translation

`POST /items/{itemId}/translations`

```json
{
  "sourceLanguage": "en",
  "targetLanguage": "es",
  "sourceText": "Repair the chipped drywall...",
  "sourceTextHash": "base64-sha256"
}
```

Returns an editable translation suggestion and revision ID. It does not modify the item until the client submits an update.

## Workflow

`POST /items/{itemId}/transitions`

```json
{
  "toStatus": "ReadyForReview",
  "note": "Correction completed; ready for review."
}
```

Requires `If-Match`. Invalid transitions return 409 with allowed transitions. For MVP, any active project member may close or reopen. A reopen request must include `"action": "Reopen"` and a non-empty reason; the server selects `Initiated` for an Observation or `Open` for a Punch List item.

## Trade partner scope

- `GET /me/items?status=&projectId=` returns only items visible through the caller's project memberships and company/trade mapping.
- Trade Partners may read item photos/descriptions/translations, comment, add completion photos and request transitions.
- They may not manage projects, Areas, Trades, company mappings, memberships or other companies' private data.

## Status codes

The 401/403/404 choice follows the resource-concealment policy in `docs/07-security-operations.md`: visibility is checked before permission, so a project or item the caller cannot see returns 404 even for an operation they would also be forbidden to perform.

- `400` validation, `401` unauthenticated
- `403` authenticated caller can see the resource but may not perform the operation
- `404` missing, or concealed because the caller has no visibility of it (same response in both cases)
- `409` invariant/idempotency conflict
- `412` ETag mismatch
- `413` photo too large, `415` unsupported media type
- `422` safe structured AI output could not be produced
- `429` throttled, `503` dependency unavailable
