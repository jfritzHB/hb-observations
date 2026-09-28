# Slice 2 recovery and completion report

Date: 2026-09-28. Scope: durable capture and photo, including the explicitly requested manual English description editor. Slice 3 was not started.

## 1. State found at takeover

The active branch was `slice-2/durable-capture-photo`, at `5816ce5`. The working tree contained eight modified tracked frontend files and nineteen untracked frontend files. Nothing was staged; no tracked files were deleted. The existing local Compose stack was running with SQL Server, Azurite and an older application image.

Read `CLAUDE.md`, `README.md` and all ten existing `docs/*.md` files before editing. Inspected status, branch, the last twenty commits, complete tracked diff, diff statistics, staged diff, untracked files, project structure and TODO/FIXME/temporary markers. No unfinished placeholder implementation was found in the Slice 2 server code. No branch switch, reset, revert, rebase, history rewrite or removal of interrupted work occurred.

## 2. Existing commits

- `5bd184e` — Add server drafts, idempotent creation and the photo pipeline.
- `5816ce5` — Test drafts, idempotency and the photo pipeline; check visibility before validation.
- Previous accepted Slice 1.1 work ends at `363e87b`; the preceding Slice 0, 1 and 1.1 history was preserved.

## 3. Uncommitted and partial work found

Modified files were the web package manifests, API client, NewItemPage and its tests, ItemsPage, routes and test setup. Untracked files implemented DraftPage, PhotoCaptureDock, PhotoPreviewSheet, photo tests, the draft model/store/sync/transport/hooks/tests, worker image processing, ID/hash helpers and synthetic test fixtures.

The frontend was already substantial and runnable: 66 tests and typecheck passed at baseline. Lint found one redundant condition. The new screens had no dedicated styling, old browser tests still expected a disabled camera, and there were no Slice 2 browser tests. Local owner isolation, storage-error handling and description persistence needed correction.

## 4. Previous agent's completed work

The two server commits already provided domain invariants, SQL mappings and migration, authoritative company snapshots, idempotency, creator-scoped Draft access, upload reservation/content/finalize/read endpoints, private Azure/Azurite storage, thumbnail generation, readiness and extensive backend tests. Compose already included Azurite and container provisioning through the migration command. `CLAUDE.md` already included Location Detail in Capture Another retention.

## 5. Work completed after takeover

Finished and committed the existing frontend implementation; added phone styles and active-camera browser assertions. Added local owner/project checks, transactional draft updates, immediate ordered description writes with failure/retry feedback, preservation of the preview on local-storage failure, replacement-photo error handling, upload timeout, server snapshot adoption, and recovery of the finalized server photo ID after a lost response.

Separated incoming upload objects from verified originals to prevent a late in-flight upload from overwriting finalized media. Kept compatibility with reservations uploaded by the previous implementation. Added bounded finalization reads, expired-upload handling, reservation-reuse auditing, and explicit concurrency conflicts rather than acknowledging an unaudited concurrent upload.

Added regression tests for local ownership, storage rejection, description edits during upload, immutable verified originals, replacement reservations and excessive pixel dimensions. Added browser tests for the real image worker, preview/removal, description/refresh, offline reconnection, server failure and lost responses at each network boundary.

## 6. Documentation changes

- `README.md`: Slice 2 setup, capture and recovery walkthroughs, storage configuration, image behavior and limits.
- `docs/05-api-contract.md`: streaming upload/read endpoints, reservation scope, replay behavior and verified-original protection.
- `docs/08-implementation-plan.md`: records the owner's explicit decision to bring local manual description entry into Slice 2.
- `docs/10-azure-deployment.md`: current storage configuration, managed-identity path, readiness and retention responsibilities.
- `.env.example` and `compose.yaml`: optional host ports for isolated verification stacks.
- This report records recovery, verification and remaining limits. `CLAUDE.md` required no change.

## 7. Final branch and commits

Branch remains `slice-2/durable-capture-photo`. Implementation completion commit: `0316524` — Complete Slice 2 durable photo capture and recovery. This report is recorded in a following documentation-only commit. Nothing was pushed or merged. No secrets, `.env`, real photos, local database/blob data, browser dumps, dependencies or test output were staged.

## 8. FieldItem and database

Retained the existing `20260928181226_AddFieldItemDrafts` migration; no additional schema change was needed. FieldItem includes Project, clientDraftId, type, Draft lifecycle, optional structured Area and path snapshot, optional Location Detail, Trade/company IDs and snapshots, priority, creator, UTC timestamps and rowversion. FieldItemPhoto includes reservation/finalization state, private blob keys, size/type/hash, verified dimensions, primary status, uploader, timestamps and rowversion.

SQL enforces draft uniqueness, same-project Area references, one active primary photo and relevant metadata constraints. Item numbers and publication remain Slice 4 work. Clean migration, model consistency and rowversion behavior were tested against SQL Server.

## 9. Idempotency

Draft creation uses both `clientDraftId` and `Idempotency-Key`, with a request fingerprint and database uniqueness. Repeated or concurrent equivalent requests return the original Draft; mismatched reuse is rejected. The existing eight-concurrent-request integration test passes. Same-byte reservations reuse the photo; finalization uses stable item/photo IDs and returns an already finalized result. Browser recovery tests replay creation and verify the same item ID and one finalized photo.

## 10. IndexedDB

Database `fieldapp-capture` stores context, owner/project, photo bytes and metadata, server IDs, upload URL, upload/finalization facts, explicit status, English text, recoverable failure and timestamps. Writes complete before network work begins. Atomic read/update transactions prevent sync progress from overwriting description edits. Draft routes check owner and project before displaying or syncing data; Items lists the current user's local captures.

## 11. Camera and file picker

Take photo uses an image file input with `capture="environment"`; Choose existing photo uses a normal picker. Preview supports Use photo, Retake, Choose different and Remove photo. Cancellation leaves context intact. Decode failures explain recovery, and a failed local save retains the preview with a retryable error. Desktop picker/browser wiring is tested; physical OS camera and permission dialogs require device testing.

## 12. Image processing

Browser decoding applies EXIF orientation; canvas encoding removes EXIF, including GPS. The long edge is capped at 2560px, without upscaling, and JPEG quality is 0.86. Worker/OffscreenCanvas handles expensive work when available, with a main-thread fallback. SHA-256 covers the exact uploaded bytes. Real browser tests process a synthetic 3200×2400 image and verify 2560px output; helper tests cover sizing and hash vectors. These settings are a quality/performance choice, not a claim that every fine defect survives compression.

## 13. Azurite and Azure Blob architecture

`IPhotoStorage` separates application logic from `AzureBlobPhotoStorage`. Local Compose uses Azurite; Azure configuration uses the storage account URL and `DefaultAzureCredential` when no storage connection string is supplied. Both use a private container. SQL stores metadata and keys only. The stateless application streams bytes without writing uploads to disk or `wwwroot`. Storage account keys and public blob URLs are not sent to the browser.

## 14. Upload, finalization and thumbnail

The sequence is durable local capture → idempotent server Draft → scoped reservation → authenticated streaming PUT → finalize → verified original and JPEG thumbnail → ready description state. Default limits are 12 MiB, 50 million pixels, a 15-minute reservation and a 480px thumbnail long edge. Finalization checks existence, length, SHA-256, signature and decodability; dimensions come from the decoder. Original and thumbnail reads enforce item authorization. Incoming objects are separate from verified originals.

## 15. Recovery and retry

Persisted facts select the next unfinished step. One runner per draft prevents duplicate work within a tab; server constraints protect request duplication. Failed drafts remain visible with their photo and context. Reopening resumes recoverable work; reconnection retries network/server failures, and a Try again button is available. Expired reservations renew, missing/mismatched bytes can be resent within a bounded recovery loop, and a lost finalized response can be recovered from the server. Local description edits survive sync completion and refresh.

## 16. Description screen

Shows the photo, compact location, type, Trade and Responsible Company; server-returned snapshots replace provisional display names after Draft creation. English is editable immediately after accepting the photo, even while sending or retrying. Text is limited to 2,000 characters and persisted locally on each change; pending/failed persistence warns on page exit. The AI control is disabled and makes no request. There is no translation, publishing or server description-save implementation.

## 17. Security and authorization

Existing 401/404/403 concealment behavior is retained and tested. A known item/photo ID grants no access; Drafts are creator-scoped within authorized project membership. Responsible Company is resolved through ProjectTrade and cannot be overridden by request fields. Server validation checks actual content independently of filename, browser MIME and dimensions. Upload URLs require authentication and an active reservation. Application audit data contains identifiers and metadata, not image bytes or credentials.

## 18. Test counts and results

Final Release solution run with Docker required and browser tests enabled: **209 passed, 0 failed, 0 skipped**. This includes **195 backend unit/integration cases and 14 Playwright cases**. Frontend: **69 passed across 7 files**.

Actually ran and passed:

- `dotnet restore FieldApp.sln`; Release build with zero warnings/errors.
- `dotnet format FieldApp.sln --verify-no-changes --no-restore`.
- Full Release .NET tests with SQL Server/Azurite Testcontainers and E2E base URL set.
- EF tool restore, pending-model check, and idempotent migration script generation to Temp.
- Clean SQL migration and schema/rowversion/idempotency integration tests.
- `npm.cmd ci` (zero reported vulnerabilities), typecheck, lint, formatting check, tests and production build.
- Production Docker image build; clean Compose SQL/Azurite/migrate/seed/app startup.
- Playwright at 360×740 with axe; refresh, connection drop, 503 failure, lost responses and duplicate prevention.
- `git diff --check` and staged whitespace check.

The initial sandbox blocked Vite child processes and Docker access; authorized execution outside that sandbox allowed the checks to run. PowerShell blocked `npm.ps1`, so `npm.cmd` was used without changing execution policy. Baseline browser skips were eliminated in the final run.

## 19. Docker, Compose and health

Fresh project `slice2-verify-20260928` created new SQL and Blob volumes, applied migrations, seeded synthetic data and became healthy at port 18080. Existing volumes were not deleted. The final application image was then started in the original Compose project at **http://localhost:8080**, with `/health/live` and `/health/ready` healthy (SQL and photo storage).

Standalone Production-mode smoke results: liveness **200**, SPA **200**, readiness without dependencies **503**, development-persona project request **401**, development-persona endpoint **404**. Temporary verification containers were stopped; their volumes were retained. The original review stack remains running.

## 20. Manual happy-path walkthrough

Open http://localhost:8080. Choose Owen Lars, open Mos Eisley Municipal Center, and select New Item. Choose a structured Area and/or enter Location Detail, select Drywall and Punch List. Choose a synthetic image; preview/change/remove it, then Use photo. Wait for the server-draft confirmation, type English text, wait for Saved on this device, and refresh. Open Items to resume the capture.

This path was exercised by Playwright and its 360px screenshots were visually inspected. A physical-phone manual walkthrough was not performed.

## 21. Manual failure/recovery walkthrough

Load capture context while online. Set DevTools Network to Offline, accept a synthetic photo, and verify that the draft reports the interruption while retaining photo/context/text. Return Online to resume. Separately block the upload or finalize request, refresh after the failure, unblock and retry. Confirm the same server item/photo IDs in Network responses. The automated suite additionally lets the server commit and deliberately loses its response at create/upload/finalize, then refreshes and retries successfully.

## 22. Known limitations

- Local protection begins when Use photo successfully writes IndexedDB; the unaccepted preview is in memory.
- Browser storage is temporary and can be cleared/evicted. No full zero-signal launch or multi-device synchronization is promised.
- English descriptions remain local. Publishing, numbering, server description editing and Capture Another after publication belong to later slices.
- HEIC/HEIF depends on browser decoder support. JPEG/PNG picker fallback is available.
- Discard removes the local capture only. Server drafts, abandoned reservations and incoming upload objects need an approved retention/cleanup policy.
- The supported browser capture path normalizes orientation and strips metadata client-side; raw API clients must supply normalized working images. Server thumbnails currently assume that normalization.
- Physical camera behavior, detailed defect visibility, Safari/iOS/Android coverage, quota/eviction behavior on real devices and Azure identity/network configuration remain pilot validation work.

## 23. Not actually verified

No Azure resources were deployed; managed identity permissions and production cloud networking were not exercised. No real construction photos, field timing study, native camera permissions, physical keyboard/OS combinations or consented AI dataset were used. Browser tests used Chromium's mobile viewport and generated geometric images. AI/translation were not called or simulated.

## 24. Specification decisions and remaining questions

The original implementation plan put manual English entry in Slice 3; the owner's explicit handoff requested it in Slice 2, so the plan now records that scope adjustment. Location Detail retention was already documented. Numbering/publish remain deferred consistently with Slice 4. No destructive ambiguity required owner input. Production retention and real-device image quality remain operational/pilot decisions; arbitrary raw API EXIF normalization is outside the implemented browser capture path and should be addressed before supporting additional upload clients.

## 25. Recommended Slice 3 plan

Preserve the manual editor and durable photo pipeline. Add an explicit optional Generate description action, server-side provider abstraction, auditable background job/status API, strict structured output validation and versioned prompts using authoritative context. Test success, slow/unavailable providers, invalid output and manual fallback without losing photo or English text. Keep translation and publishing in Slice 4. None of this Slice 3 implementation was started during recovery.
