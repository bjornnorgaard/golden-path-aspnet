# Entity Framework Core (PostgreSQL)

Design-time tools use `WebApi.Database.AppDbContextFactory`. Set *
*`ConnectionStrings__DefaultConnection`** when the default in `appsettings` is
wrong (same value as in `appsettings.json`).

`dotnet-ef` is pinned in **`src/dotnet-tools.json`**. Restore the tool once from
**`src`**:

```bash
cd src && dotnet tool restore
```

---

## From `src/WebApi` (project root)

`dotnet ef` discovers **`WebApi.csproj`** automatically.

### Add a migration

```bash
cd src/WebApi
pwd
dotnet tool run dotnet-ef -- migrations add Initial --output-dir Database/Migrations --project ../WebApi.csproj
```

### Remove / list / update / script

```bash
cd src/WebApi
dotnet tool run dotnet-ef -- migrations remove
dotnet tool run dotnet-ef -- migrations list
dotnet tool run dotnet-ef -- database update
dotnet tool run dotnet-ef -- database update <MigrationName>
dotnet tool run dotnet-ef -- migrations script
dotnet tool run dotnet-ef -- migrations script <FromMigration> <ToMigration>
dotnet tool run dotnet-ef -- dbcontext info
dotnet tool run dotnet-ef -- dbcontext list
```

Migrations are applied automatically when the application starts. The database
must be reachable with the configured `ConnectionStrings__DefaultConnection`.

---

## From `src/WebApi/Database`

This directory has **no `.csproj`**, so every command needs **`--project`** and
**`--startup-project`** pointing at the WebApi project:

```bash
cd src/WebApi/Database
dotnet tool run dotnet-ef -- migrations add <MigrationName> \
  --project ../WebApi.csproj --startup-project ../WebApi.csproj \
  --output-dir Database/Migrations
```

Same flags on other commands, for example:

```bash
dotnet tool run dotnet-ef -- migrations remove --project ../WebApi.csproj --startup-project ../WebApi.csproj
dotnet tool run dotnet-ef -- migrations list --project ../WebApi.csproj --startup-project ../WebApi.csproj
dotnet tool run dotnet-ef -- database update --project ../WebApi.csproj --startup-project ../WebApi.csproj
dotnet tool run dotnet-ef -- migrations script --project ../WebApi.csproj --startup-project ../WebApi.csproj
```
