FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
ARG APP_UID=1000
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Generators/Generators/Generators.csproj", "Generators/Generators/"]
RUN dotnet restore "WebApi/WebApi.csproj"
COPY . .
RUN dotnet restore "WebApi/WebApi.csproj" -r linux-x64
WORKDIR "/src/WebApi"
RUN dotnet build "./WebApi.csproj" -c $BUILD_CONFIGURATION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/WebApi"
RUN dotnet publish "./WebApi.csproj" -c $BUILD_CONFIGURATION -r linux-x64 -o /app/publish /p:UseAppHost=true

FROM base AS final
ARG APP_UID=1000
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID --chmod=755 /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["/app/WebApi"]
