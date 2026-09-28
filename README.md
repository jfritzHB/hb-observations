# Construction Field Capture Application

Build-ready design package for a mobile-first application used by construction superintendents to record observations and punch-list items from a phone or tablet.

## Product goal

A superintendent can create a usable field item in under 30 seconds:

1. Choose a project area.
2. Choose the responsible trade.
3. Choose **Observation** or **Punch List**.
4. Take a photo.
5. Review and edit an AI-drafted English description.
6. Optionally generate and edit a Spanish translation.
7. Save the item.

AI is an assistant only. The user always reviews the description, trade and translation before publication.

Trade partners are included in the MVP. Selecting a project Trade automatically identifies the responsible trade partner through project configuration; the field user does not separately select a subcontractor.

## Recommended MVP

- React + TypeScript mobile-first PWA
- ASP.NET Core 10 Web API (use .NET 8 if the deployment environment requires LTS)
- EF Core + Azure SQL
- Azure Blob Storage for photos
- Microsoft Entra ID for employee authentication
- Azure OpenAI multimodal model behind a server-side abstraction
- Docker image deployable to Azure Container Apps
- Online-first capture with durable local drafts; full offline synchronization is Phase 2

## Document map

| File | Purpose |
| --- | --- |
| `CLAUDE.md` | Mandatory operating instructions for Claude Code |
| `docs/01-product-requirements.md` | Scope, roles, workflows and acceptance criteria |
| `docs/02-mobile-ux.md` | Screen behavior and field usability rules |
| `docs/03-architecture.md` | System structure and engineering decisions |
| `docs/04-domain-model.md` | Entities, enums, constraints and state rules |
| `docs/05-api-contract.md` | REST endpoints and example payloads |
| `docs/06-ai-media.md` | Image analysis, translation and photo pipeline |
| `docs/07-security-operations.md` | Authorization, audit, privacy and deployment |
| `docs/08-implementation-plan.md` | Vertical slices, tests and definition of done |
| `docs/09-open-decisions.md` | Business questions that must be confirmed |
| `docs/10-azure-deployment.md` | Docker and Azure hosting design |
| `Dockerfile` | Production multi-stage React and .NET container build |
| `compose.yaml` | Local application and SQL Server environment |
| `.dockerignore` | Container build exclusions |
| `.env.example` | Configuration names with no secrets |

## Suggested repository layout

```text
src/
  FieldApp.Api/
  FieldApp.Application/
  FieldApp.Domain/
  FieldApp.Infrastructure/
  FieldApp.Web/
tests/
  FieldApp.UnitTests/
  FieldApp.IntegrationTests/
  FieldApp.E2ETests/
docs/
```

Start with `CLAUDE.md`, then implement Slice 0 and Slice 1 in `docs/08-implementation-plan.md`.

## Local development

Prerequisites: Docker, the .NET 10 SDK and Node.js 22.12 or later (the last two are only needed to work outside containers).

On Windows, clone to a short path (or enable long paths): the Playwright package copies deeply nested files into `tests/FieldApp.E2ETests/bin` and the build fails if the full path exceeds 260 characters.

### Run everything in containers (one command)

Create a local `.env` once, setting `FIELDAPP_SQL_PASSWORD` to a strong password of your own (SQL Server requires upper/lower case, digits and symbols). `.env` is git-ignored; never commit it.

```bash
cp .env.example .env   # then edit FIELDAPP_SQL_PASSWORD
```

Then:

```bash
docker compose up --build
```

- App (API and compiled React app): <http://localhost:8080>
- Liveness: <http://localhost:8080/health/live>; readiness, including SQL and private Blob Storage: <http://localhost:8080/health/ready>
- OpenAPI document (Development only): <http://localhost:8080/openapi/v1.json>
- SQL Server: `localhost,14333` (user `sa`)
- Azurite Blob service: `localhost:10000`; private photos live in the `fieldapp-blobs` Docker volume.

Before the app starts, the one-shot `migrate` service runs the same image with `migrate --seed-demo-data`. It applies EF Core migrations (creating the database if needed) and loads the synthetic demo data, then exits. This mirrors the production design: migrations run as a separate, controlled step (a Container Apps Job or pipeline step), never at application startup.

### Run the API and web app natively

```bash
docker compose up -d sql azurite                           # local dependencies
dotnet user-secrets set ConnectionStrings:AppDb "Server=localhost,14333;Database=FieldApp;User Id=sa;Password=<your password>;Encrypt=True;TrustServerCertificate=True" --project src/FieldApp.Api
# Also set ConnectionStrings:PhotoStorage to the Azurite connection string in compose.yaml,
# changing BlobEndpoint to http://localhost:10000/devstoreaccount1 for a native API process.
dotnet run --project src/FieldApp.Api -- migrate --seed-demo-data   # apply migrations + synthetic data, then exit
dotnet run --project src/FieldApp.Api --launch-profile http # http://localhost:5001

cd src/FieldApp.Web
npm install
npm run dev                                              # http://localhost:5173, proxies /api and /health to the API
```

### Development authentication (synthetic personas)

Until Microsoft Entra ID is integrated, local development uses a persona stub: the client sends `X-Dev-Persona: <key>` and the server treats that as the signed-in identity. There are no passwords. The stub produces the same identity claims that Entra will, and everything downstream (application-user resolution, project membership authorization) is scheme-independent.

- It is enabled only by `Authentication:Mode=Development` (set in `appsettings.Development.json`). The API **refuses to start** if that mode is configured in any environment other than Development. Without it, every `/api/v1` request returns 401.
- In the app, choose a persona on first load, or switch under **More → Development persona**. With curl: `curl -H "X-Dev-Persona: superintendent" http://localhost:8080/api/v1/projects`.
- `GET /api/v1/dev/personas` lists the personas, and exists only in that mode.

| Persona key | Name | Access (all data is fictional) |
| --- | --- | --- |
| `superintendent` | Owen Lars | Superintendent on HB-TEST-001 and HB-TEST-002 |
| `project-manager` | Beru Whitesun | Project Manager on HB-TEST-001 (can add areas); *inactive* membership on HB-TEST-002 |
| `administrator` | Wedge Antilles | Administrator on all three projects |
| `trade-partner` | Wuher Dunesea | Trade Partner for Dune Sea Drywall Co. on HB-TEST-001 only |
| `unassigned` | Biggs Darklighter | Provisioned user with no memberships |

Demo projects: **HB-TEST-001 Mos Eisley Municipal Center** (area hierarchy Building A/B with levels and rooms, seven capture-ready trades, and deliberately unavailable ones: Roofing unmapped, Concrete disabled, Glazing mapped to an inactive company, Fireproofing inactive), **HB-TEST-002 Anchorhead Water Treatment Plant** and **HB-TEST-003 Tosche Station Retrofit**.

### Database migrations

```bash
dotnet tool restore                                            # installs the pinned dotnet-ef
dotnet ef migrations add <Name> --project src/FieldApp.Infrastructure --startup-project src/FieldApp.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/FieldApp.Infrastructure --startup-project src/FieldApp.Infrastructure
dotnet ef migrations script --idempotent --project src/FieldApp.Infrastructure --startup-project src/FieldApp.Infrastructure --output migrations.sql
docker compose run --rm migrate                                # apply to the local Compose database
```

`/health/ready` reports unhealthy until every migration in the running build has been applied.

### Manually testing capture (Slice 2)

1. `docker compose up --build`, then open <http://localhost:8080> (use the browser's device toolbar at 360px wide, or a phone on the same network).
2. Choose **Owen Lars (Superintendent)**. Projects shows HB-TEST-001 and HB-TEST-002 only.
3. Open **Mos Eisley Municipal Center** and tap **New Item**.
4. In **Where?** type `201` and tap **Building A / Level 2 / Office 201**; then type `North wall` as extra location detail. (Or type `Unit 214` alone: free-text location detail never creates an Area. **Browse areas** chooses without typing.)
5. Tap **Drywall**. **Responsible: Dune Sea Drywall Co.** appears immediately; there is no company picker.
6. Tap **Punch List**, then **Take photo** (camera on supported phones) or **Choose existing photo**. Use a synthetic image. Preview, retake/change/remove as needed, then tap **Use photo**. That action protects the processed photo and context in IndexedDB before any server call.
7. Authorization boundary: **More → Beru Whitesun (Project Manager)** now shows only HB-TEST-001. Opening an HB-TEST-002 URL shows "Project not found". `curl -i -H "X-Dev-Persona: superintendent" -X POST -H "Content-Type: application/json" -d '{"name":"Roof"}' http://localhost:8080/api/v1/projects/<HB-TEST-001 id>/areas` returns 403, and the same call for HB-TEST-003 returns 404.

Recent locations (area and/or detail) and trades are remembered on the device per user and project (browser storage), because server-side recency needs captured items, which later slices add.

After **Use photo**, the app creates an idempotent server Draft, streams the reserved upload into private Blob Storage, and verifies/finalizes the photo and thumbnail. **Saved to the server as a draft** confirms the photo pipeline. English description is editable immediately, including during an interrupted upload; each edit is stored on this device. Wait for **Saved on this device** before refreshing. Refresh restores the photo, description and progress. **Items** lists captures on this device.

Failure/recovery: after loading the capture screen, use browser DevTools Network **Offline**, accept a photo, and confirm the local failure status. Return **Online** to resume automatically, or tap **Try again**. For refresh recovery, block the upload or finalize URL, accept a photo, refresh, then unblock and retry. The same item and photo IDs are reused. No AI, translation, publish or server description save is implemented in Slice 2.

Photo processing applies orientation, caps the long edge at 2560px (without upscaling), encodes JPEG at quality 0.86, and removes EXIF metadata. Processing runs in a Web Worker when supported. HEIC/HEIF requires browser decoding support; choose JPEG/PNG if the browser cannot read it. Fine-defect image quality still needs physical-device field validation.

Local preview becomes durable only after **Use photo** succeeds. Browser storage can be cleared or evicted; it is temporary interruption protection, not a backup or full offline application. Discard removes the local copy only; server drafts and abandoned upload objects await an approved retention policy. Descriptions are not yet stored on the server.

For a separate clean verification stack, set `FIELDAPP_APP_PORT=18080`, `FIELDAPP_SQL_PORT=24333` and `FIELDAPP_BLOB_PORT=11000`, then run `docker compose -p slice2-verification up --build --detach --wait app`. A new project name gets fresh SQL and Azurite volumes without deleting existing data. On PowerShell use `$env:NAME='value'` and `npm.cmd` if execution policy blocks `npm.ps1`.

### Checks (the same ones CI runs)

```bash
dotnet format FieldApp.sln --verify-no-changes
dotnet build FieldApp.sln
dotnet test --solution FieldApp.sln   # SQL Server integration tests use Testcontainers (Docker); they are skipped without Docker unless FIELDAPP_REQUIRE_DOCKER=true

cd src/FieldApp.Web
npm run typecheck && npm run lint && npm run format:check && npm test && npm run build
```

Browser (Playwright + axe accessibility) tests run at a 360px phone viewport against a running, seeded instance (`docker compose up`), and are skipped unless `FIELDAPP_E2E_BASE_URL` is set. Set `FIELDAPP_E2E_SCREENSHOTS=<dir>` to save phone screenshots for review.

```bash
pwsh tests/FieldApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium   # once
FIELDAPP_E2E_BASE_URL=http://localhost:8080 dotnet test --project tests/FieldApp.E2ETests
```

### Production image

```bash
docker build -t fieldapp .
docker run --rm -p 8080:8080 fieldapp   # /health/live returns 200; /health/ready returns 503 until ConnectionStrings__AppDb is supplied
```

## Hosting target

The production container is stateless and listens on port `8080`, making it suitable for Azure Container Apps or Azure App Service for Containers. Azure SQL stores relational data and private Azure Blob Storage stores photos. Persistent application data must never be written inside the container filesystem.
