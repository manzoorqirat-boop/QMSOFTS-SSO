# QMSofts.Identity — multi-stage build.
# Build context must be the SOLUTION ROOT (the folder containing /src),
# because this project references ../QMSofts.Shared.
# On Railway set the service Root Directory to the repo root and point
# Dockerfile path at src/QMSofts.Identity/Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy project files first for layer-cached restore.
COPY src/QMSofts.Shared/QMSofts.Shared.csproj src/QMSofts.Shared/
COPY src/QMSofts.Identity/QMSofts.Identity.csproj src/QMSofts.Identity/
RUN dotnet restore src/QMSofts.Identity/QMSofts.Identity.csproj

# Copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/QMSofts.Identity/QMSofts.Identity.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Railway injects PORT; the app binds to it. EXPOSE is documentation only.
EXPOSE 8080
ENTRYPOINT ["dotnet", "QMSofts.Identity.dll"]
