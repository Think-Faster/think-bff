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

# RUN addgroup -S app && adduser -S app -G app
# USER app

COPY --from=build /app ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "BFF.WebApi.dll"]
