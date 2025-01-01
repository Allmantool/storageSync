FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /scr
COPY ["StorageSyncWorker.csproj", "."]
RUN dotnet restore "./StorageSyncWorker.csproj"
COPY . .
RUN dotnet build StorageSyncWorker.sln -c Release --no-incremental --framework:net9.0 -maxcpucount:1 -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
RUN dotnet publish StorageSyncWorker.sln \
    --no-dependencies \
    --no-restore \
    --framework net9.0 \
    -c Release \
    -v Diagnostic \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StorageSyncWorker.dll"]