FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM debian:bookworm-slim AS tectonic
ARG TECTONIC_VERSION=0.17.0
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL "https://github.com/tectonic-typesetting/tectonic/releases/download/tectonic%40${TECTONIC_VERSION}/tectonic-${TECTONIC_VERSION}-x86_64-unknown-linux-musl.tar.gz" \
    | tar -xz -C /usr/local/bin tectonic \
    && chmod +x /usr/local/bin/tectonic

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore

FROM build AS publish
RUN dotnet publish src/LatexEditor.Api/LatexEditor.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
# fontconfig + a base font set: Tectonic errors without a fontconfig config
# and fontspec documents need real system fonts.
RUN apt-get update && apt-get install -y --no-install-recommends \
        fontconfig fonts-dejavu-core fonts-liberation \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=tectonic /usr/local/bin/tectonic /usr/local/bin/tectonic
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LatexEditor.Api.dll"]
