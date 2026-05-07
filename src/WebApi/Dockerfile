FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG APP_UID=1000
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["dotnet-tools.json", "."]
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Platform/Platform.csproj", "Platform/"]
COPY ["Generators/Generators/Generators.csproj", "Generators/Generators/"]
RUN dotnet restore "WebApi/WebApi.csproj"
COPY . .
RUN dotnet restore "WebApi/WebApi.csproj" -r linux-x64
RUN dotnet tool restore
WORKDIR "/src/WebApi"
RUN dotnet build "./WebApi.csproj" -c $BUILD_CONFIGURATION

FROM build AS bundle
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/WebApi"
RUN dotnet tool run dotnet-ef -- migrations bundle \
    --self-contained \
    --target-runtime linux-x64 \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/efbundle

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/WebApi"
RUN dotnet publish "./WebApi.csproj" -c $BUILD_CONFIGURATION -r linux-x64 -o /app/publish /p:UseAppHost=true

FROM base AS final
ARG APP_UID=1000
USER root
WORKDIR /app
COPY --from=publish /app/publish/WebApi ./WebApi
COPY --from=bundle /app/efbundle ./efbundle
COPY WebApi/entrypoint.sh ./entrypoint.sh
RUN chmod +x ./entrypoint.sh ./WebApi ./efbundle \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["/app/entrypoint.sh"]
