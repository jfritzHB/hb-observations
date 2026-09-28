# syntax=docker/dockerfile:1

FROM node:22-alpine AS web-build
WORKDIR /src/FieldApp.Web
COPY src/FieldApp.Web/package*.json ./
RUN npm ci
COPY src/FieldApp.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY . .
RUN dotnet restore FieldApp.sln
RUN dotnet publish src/FieldApp.Api/FieldApp.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=api-build /app/publish ./
COPY --from=web-build /src/FieldApp.Web/dist ./wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "FieldApp.Api.dll"]
