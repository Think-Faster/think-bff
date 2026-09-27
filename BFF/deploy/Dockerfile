# Build context is the solution root (BFF/); build with:
#   docker build -f deploy/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/BFF.Models/BFF.Models.csproj src/BFF.Models/
COPY src/BFF.Contracts/BFF.Contracts.csproj src/BFF.Contracts/
COPY src/BFF.Context/BFF.Context.csproj src/BFF.Context/
COPY src/BFF.Application/BFF.Application.csproj src/BFF.Application/
COPY src/BFF.Infrastructure/BFF.Infrastructure.csproj src/BFF.Infrastructure/
COPY src/BFF.WebApi/BFF.WebApi.csproj src/BFF.WebApi/
RUN dotnet restore src/BFF.WebApi/BFF.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/BFF.WebApi/BFF.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# bash/curl/jq — needed by vault-entrypoint.sh (Vault ТЗ, see docs/VAULT_MIGRATION.md). No secrets are
# ever baked into this image: ARG/ENV with real values here is forbidden by that same spec.
RUN apk add --no-cache bash curl jq

# RUN addgroup -S app && adduser -S app -G app
# USER app

COPY --from=build /app ./

COPY vault-entrypoint.sh /usr/local/bin/vault-entrypoint.sh
RUN chmod +x /usr/local/bin/vault-entrypoint.sh

EXPOSE 8080
# vault-entrypoint.sh loads secrets from Vault into the environment (or, without VAULT_ROLE_ID, does
# nothing) and then execs the real command below — application code is unchanged, it still just reads
# environment variables.
ENTRYPOINT ["/usr/local/bin/vault-entrypoint.sh"]
CMD ["dotnet", "BFF.WebApi.dll"]
