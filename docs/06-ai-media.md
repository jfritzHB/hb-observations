# AI and Media Design

## Principles

The model drafts text from visible evidence and supplied context. It does not determine legal responsibility, hidden conditions, code compliance, severity, or whether work is safe. User-selected Area, Trade and Type are authoritative inputs.

## Analysis input

- One or more finalized photos via short-lived server-authorized access
- Item Type
- Area path
- Selected Trade
- Optional user note
- Organization terminology and allowed output language

Do not send user identity, customer contact information, unrelated project data or public blob URLs.

## Structured output

```json
{
  "title": "string, 1-80 chars",
  "descriptionEnglish": "string, 1-2000 chars",
  "possibleTradeMismatch": false,
  "suggestedTradeName": null,
  "uncertaintyNote": null,
  "visibleEvidenceOnly": true
}
```

Use strict JSON schema structured output when supported. Validate again server-side. Reject prompt-injection text visible in images or notes as instructions; treat it only as scene content.

## Description prompt contract

The system prompt must direct the model to:

- Describe only what is clearly visible and relevant to the selected item type.
- Use concise, neutral construction language and an actionable correction only when justified.
- Avoid naming people, assigning blame, estimating cost, declaring code violations or claiming unsafe conditions.
- Avoid inventing dimensions, materials or locations.
- State uncertainty briefly when the image is insufficient.
- Respect the selected trade; flag a possible mismatch separately rather than replacing it.
- Return only the required schema.

## Translation contract

Translation is a separate operation using the final current English text. The prompt must preserve meaning, measurements, product names and construction terminology; produce neutral professional Spanish; and return only `{ "translatedText": "..." }`. Do not append Spanish to English. Store source hash and model/prompt version.

## Safety and quality controls

- Block unsupported formats and enforce configurable file size/pixel limits.
- Scan uploads according to organizational security policy before analysis.
- Remove GPS EXIF by default; retain captured time only if policy approves.
- Moderate user notes and model output where required by policy.
- Log request IDs, latency, outcome, model deployment and template version, but not secrets or unrestricted photo URLs.
- Rate limit per user/project and cap retries/cost.
- Provide manual fallback for every model failure.

## Evaluation set

Before pilot, create a consented, nonproduction test set covering drywall, paint, electrical, plumbing, flooring, concrete, doors/hardware, ambiguous images, clean/no-defect images, multiple defects, poor light and Spanish construction terminology. Score factuality, invented details, trade mismatch flag, usability, translation fidelity and latency. Do not use live customer photos as a training set without explicit policy approval.

## Media lifecycle

1. Client captures and locally compresses a working copy.
2. Server creates a constrained upload reservation.
3. Client uploads with progress.
4. Server verifies type/size/hash and normalizes orientation.
5. Server creates thumbnail and marks media finalized.
6. AI worker receives internal time-limited read access.
7. Retention/deletion follows approved project policy and legal holds.
