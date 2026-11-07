# syntax=docker/dockerfile:1

# ---- restore (cached until a csproj or package file changes) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Trail.slnx dotnet-tools.json ./
COPY src/Trail.Domain/Trail.Domain.csproj src/Trail.Domain/
COPY src/Trail.Application/Trail.Application.csproj src/Trail.Application/
COPY src/Trail.Infrastructure/Trail.Infrastructure.csproj src/Trail.Infrastructure/
COPY src/Trail.Api/Trail.Api.csproj src/Trail.Api/
RUN dotnet restore src/Trail.Api/Trail.Api.csproj
COPY src/ src/
RUN dotnet publish src/Trail.Api -c Release -o /app --no-restore /p:UseAppHost=false

# ---- EF Core migrations bundle (target: migrator) ----
FROM build AS bundle
RUN dotnet tool restore
RUN dotnet ef migrations bundle -p src/Trail.Infrastructure -s src/Trail.Api \
    -o /bundle/efbundle --self-contained -r linux-x64 --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /migrator
COPY --from=bundle /bundle/efbundle ./efbundle
USER $APP_UID
# Requires Database__ConnectionString at run time.
ENTRYPOINT ["sh", "-c", "exec ./efbundle --connection \"$Database__ConnectionString\""]

# ---- runtime (default target) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/App_Data/uploads && chown -R $APP_UID:$APP_UID /app/App_Data
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    Storage__LocalPath=/app/App_Data/uploads
EXPOSE 8080
USER $APP_UID
VOLUME ["/app/App_Data/uploads"]
ENTRYPOINT ["dotnet", "Trail.Api.dll"]
