# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching on rebuilds
COPY src/WorkLens.Api/WorkLens.Api.csproj src/WorkLens.Api/
COPY src/WorkLens.Core/WorkLens.Core.csproj src/WorkLens.Core/
COPY src/WorkLens.Infrastructure/WorkLens.Infrastructure.csproj src/WorkLens.Infrastructure/
RUN dotnet restore src/WorkLens.Api/WorkLens.Api.csproj

COPY src/ src/
RUN dotnet publish src/WorkLens.Api/WorkLens.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WorkLens.Api.dll"]
