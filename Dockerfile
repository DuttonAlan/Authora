# syntax=docker/dockerfile:1

# Builds two images from the same sources:
#   docker build --target runtime  -t authora .            # the server (default target)
#   docker build --target migrator -t authora-migrator .   # applies the database migrations, then exits
#
# Authora never migrates the database at startup, so run the migrator before every new version.

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /source

# Restore in its own layer, so it is only repeated when a project file changes.
COPY global.json Authora.slnx .editorconfig ./
COPY src/Authora.Api/Authora.Api.csproj src/Authora.Api/
COPY src/Authora.Core/Authora.Core.csproj src/Authora.Core/
COPY src/Authora.Infrastructure/Authora.Infrastructure.csproj src/Authora.Infrastructure/
RUN dotnet restore src/Authora.Api/Authora.Api.csproj

COPY src/ src/
RUN dotnet publish src/Authora.Api/Authora.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM build AS bundle
ARG DOTNET_EF_VERSION=10.0.12
RUN dotnet tool install --global dotnet-ef --version ${DOTNET_EF_VERSION}
ENV PATH="${PATH}:/root/.dotnet/tools"
# Creating the bundle builds the DbContext, which needs a connection string; it never connects. The real one is
# passed to the bundle when it runs.
RUN ConnectionStrings__Database="Host=localhost;Database=bundle" \
    dotnet ef migrations bundle \
        --project src/Authora.Infrastructure \
        --startup-project src/Authora.Api \
        --configuration Release \
        --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle ./efbundle
# The bundle starts the host of Authora.Api to find the DbContext. Outside Development, Authora refuses to start
# with AllowedHosts=*, so a placeholder is set here; the migrator serves no requests.
ENV AllowedHosts=migrator
USER $APP_UID
# Pass the connection string of the database owner like for the server, e.g. as the environment variable
# ConnectionStrings__Database=Host=…;Database=authora;Username=…;Password=…
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# libgssapi-krb5-2: Npgsql loads it at startup and prints an error without it.
# /var/lib/authora/keys: default directory for Authora:DataProtection:KeysPath; mount a volume here.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/authora/keys \
    && chown "$APP_UID" /var/lib/authora/keys

COPY --from=build /app/publish .

# Plain HTTP on 8080 for a TLS-terminating reverse proxy (see docs/how-to/configure-a-reverse-proxy.md).
# To let Kestrel terminate TLS instead, set ASPNETCORE_URLS and Kestrel__Certificates__Default__*.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Authora__DataProtection__KeysPath=/var/lib/authora/keys
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Authora.Api.dll"]
