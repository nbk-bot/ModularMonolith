# Multi-stage Dockerfile for ModularMonolith Api.
#
# Build (from the solution root, two levels up):
#   docker build -f src/Api/Dockerfile -t modular-monolith-api .
#
# Run:
#   docker run --rm -p 8080:8080 modular-monolith-api

# --- build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + every .csproj first so `dotnet restore` is cached when only sources change.
COPY ModularMonolith.slnx ./
COPY src/Api/Api.csproj                                                            src/Api/
COPY src/BuildingBlocks/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj         src/BuildingBlocks/BuildingBlocks.Domain/
COPY src/BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj src/BuildingBlocks/BuildingBlocks.Application/
COPY src/BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/BuildingBlocks.Infrastructure/
COPY src/Modules/Identity/Identity.Domain/Identity.Domain.csproj                   src/Modules/Identity/Identity.Domain/
COPY src/Modules/Identity/Identity.Application/Identity.Application.csproj         src/Modules/Identity/Identity.Application/
COPY src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj   src/Modules/Identity/Identity.Infrastructure/
COPY src/Modules/Identity/Identity.Presentation/Identity.Presentation.csproj       src/Modules/Identity/Identity.Presentation/
COPY src/Modules/Catalog/Catalog.Domain/Catalog.Domain.csproj                      src/Modules/Catalog/Catalog.Domain/
COPY src/Modules/Catalog/Catalog.Application/Catalog.Application.csproj            src/Modules/Catalog/Catalog.Application/
COPY src/Modules/Catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj      src/Modules/Catalog/Catalog.Infrastructure/
COPY src/Modules/Catalog/Catalog.Presentation/Catalog.Presentation.csproj          src/Modules/Catalog/Catalog.Presentation/

RUN dotnet restore src/Api/Api.csproj

# Now copy the rest of the sources and publish.
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

# --- runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
