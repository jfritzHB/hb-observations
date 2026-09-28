# Azure Docker Deployment

## Recommended topology

- Azure Container Registry stores immutable application images.
- Azure Container Apps runs the stateless web/API container on port 8080.
- Azure SQL Database stores application records.
- Private Azure Blob Storage stores photos and thumbnails.
- Azure OpenAI provides image analysis and translation.
- Key Vault stores secrets when managed identity cannot replace them.
- Application Insights and Log Analytics collect telemetry.
- A Container Apps Job or controlled pipeline step applies EF Core migrations.

The compiled React PWA is served by ASP.NET Core from `wwwroot`, producing one deployable image and avoiding cross-origin configuration for the MVP.

## Container contract

- Listen on `0.0.0.0:8080` through `ASPNETCORE_URLS`.
- Serve `GET /health/live` without checking external dependencies.
- Serve `GET /health/ready` after checking critical configuration and database connectivity with a short timeout.
- Write logs to stdout/stderr in structured form.
- Store no business data, uploaded photos, keys or mutable state in the container filesystem.
- Handle forwarded headers from the Azure ingress proxy.
- Shut down gracefully and honor cancellation tokens.

## Required application behavior

The API project must serve static files, use SPA fallback to `index.html`, trust forwarded headers from known platform networks, and map health endpoints. The PWA build must receive only browser-safe configuration. Secrets remain server-side.

## Deployment sequence

1. Build and test backend and frontend.
2. Build the Docker image using the repository `Dockerfile`.
3. Run the image locally and smoke-test `/health/live`.
4. Push an immutable commit-tagged image to Azure Container Registry.
5. Apply reviewed EF Core migrations using the same source revision.
6. Create a new Container Apps revision with runtime configuration and managed identity.
7. Verify readiness, authentication, photo upload, AI fallback and database access in the new revision.
8. Shift traffic to the new revision; retain the prior healthy revision for rollback.

Do not use container startup to apply production migrations. A failed or concurrent startup migration can prevent safe scale-out and rollback.

## Runtime configuration

Configure the values represented in `.env.example` through Container Apps environment variables and secret references. Prefer managed identity for Blob Storage, Key Vault and Azure OpenAI when supported. Restrict Azure SQL networking and credentials according to the organization's Azure policy.

## Storage and uploads

Photos must go directly to Blob Storage through constrained upload grants or through a streaming API endpoint. Never place uploads under `wwwroot` or another container directory. Container replacement, scale-out and restart must not lose photos.

## Scale and cost controls

Begin with one minimum replica for reliable interactive access if budget permits. Configure conservative maximum replicas and concurrency. Set Azure OpenAI rate/cost limits, blob lifecycle policies and SQL service tier deliberately. Do not enable scale-to-zero until cold-start behavior has been tested with field users.

## Production readiness checklist

- Custom domain and TLS configured
- Employee and external Trade Partner redirect URLs registered
- Managed identity permissions verified
- SQL backups and restoration tested
- Blob CORS/upload rules restricted
- Secrets absent from image and logs
- Health probes configured
- Application Insights alerts configured
- Migration and rollback procedures rehearsed
- Synthetic test users/photos only in nonproduction
