# syntax=docker/dockerfile:1
# SkyJourney API image. Build from the backend directory:
#   docker build -t skyjourney-api .                      -> the API (final stage)
#   docker build --target migrations -t skyjourney-migrations .  -> EF Core migration bundle (release step)
# The image is built for the platform of the Docker engine that builds it (CI builds linux/amd64).

ARG DOTNET_VERSION=10.0

# ---- build -------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/SkyJourney.Domain/SkyJourney.Domain.csproj src/SkyJourney.Domain/
COPY src/SkyJourney.Application/SkyJourney.Application.csproj src/SkyJourney.Application/
COPY src/SkyJourney.Infrastructure/SkyJourney.Infrastructure.csproj src/SkyJourney.Infrastructure/
COPY src/SkyJourney.Api/SkyJourney.Api.csproj src/SkyJourney.Api/
RUN dotnet restore src/SkyJourney.Api/SkyJourney.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/SkyJourney.Api/SkyJourney.Api.csproj -c Release --no-restore -o /app/publish \
    /p:UseAppHost=false

# ---- migrations --------------------------------------------------------------------------------------------------
# A single executable that applies pending migrations (idempotent). Run it once per release, before the new API version
# starts:  docker run --rm -e ConnectionStrings__SkyJourney="..." skyjourney-migrations
FROM build AS migrations-build
RUN dotnet ef migrations bundle --project src/SkyJourney.Infrastructure --startup-project src/SkyJourney.Infrastructure \
    --configuration Release --output /app/efbundle --force

# The bundle needs the ASP.NET Core shared framework (Identity), so it uses the same base image as the API.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS migrations
WORKDIR /app
COPY --from=migrations-build /app/efbundle ./efbundle
USER $APP_UID
# The connection string comes from the environment, never from the image.
ENTRYPOINT ["/bin/sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__SkyJourney\""]

# ---- final -------------------------------------------------------------------------------------------------------
# The default (Ubuntu-based) image ships ICU and tzdata, which the API needs for IANA time zones (airport local times).
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish ./
# Non-root user provided by the .NET images.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SkyJourney.Api.dll"]
