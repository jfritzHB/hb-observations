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
- Liveness: <http://localhost:8080/health/live>; readiness, including the database: <http://localhost:8080/health/ready>
- OpenAPI document (Development only): <http://localhost:8080/openapi/v1.json>
- SQL Server: `localhost,14333` (user `sa`)

The `sql-init` service creates the empty `FieldApp` database for local use. Schema changes are applied by a separate, controlled migration step, never by application startup.

### Run the API and web app natively

```bash
docker compose up -d sql sql-init                        # database only
dotnet user-secrets set ConnectionStrings:AppDb "Server=localhost,14333;Database=FieldApp;User Id=sa;Password=<your password>;Encrypt=True;TrustServerCertificate=True" --project src/FieldApp.Api
dotnet run --project src/FieldApp.Api --launch-profile http # http://localhost:5001

cd src/FieldApp.Web
npm install
npm run dev                                              # http://localhost:5173, proxies /api and /health to the API
```

### Checks (the same ones CI runs)

```bash
dotnet format FieldApp.sln --verify-no-changes
dotnet build FieldApp.sln
dotnet test --solution FieldApp.sln   # SQL Server integration tests use Testcontainers (Docker); they are skipped without Docker unless FIELDAPP_REQUIRE_DOCKER=true

cd src/FieldApp.Web
npm run typecheck && npm run lint && npm run format:check && npm test && npm run build
```

Browser (Playwright) tests run against a running instance and are skipped unless `FIELDAPP_E2E_BASE_URL` is set:

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
