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

## Hosting target

The production container is stateless and listens on port `8080`, making it suitable for Azure Container Apps or Azure App Service for Containers. Azure SQL stores relational data and private Azure Blob Storage stores photos. Persistent application data must never be written inside the container filesystem.
