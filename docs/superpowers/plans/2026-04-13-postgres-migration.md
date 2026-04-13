# PostgreSQL Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Microsoft SQL Server with PostgreSQL as the persistence engine for the asset tracker, using `TIMESTAMPTZ` for datetimes and PostgreSQL's default `snake_case` casing convention.

**Architecture:** Swap `Microsoft.Data.SqlClient` for `Npgsql` behind the existing `ConnectionFactory` abstraction. Rewrite the two existing migration scripts in-place as PostgreSQL DDL. Rewrite repository SQL to use `RETURNING` instead of `OUTPUT INSERTED`, `BOOLEAN` instead of `BIT`, and `snake_case` identifiers. Bridge snake_case DB columns to PascalCase C# properties via Dapper's `DefaultTypeMap.MatchNamesWithUnderscores`. Promote domain datetime properties from `DateTime` to `DateTimeOffset` to match `TIMESTAMPTZ` semantics. Keep credentials entirely out of the repo: the CLI uses `dotnet user-secrets` for its connection string; the test project reads its connection string from an environment variable.

**Tech Stack:** .NET 10, Dapper 2.1.x, Npgsql (latest stable), `Microsoft.Extensions.Configuration.UserSecrets`, PostgreSQL 16+ (target), NUnit.

**Assumptions (call out and confirm before execution):**
- No production data needs preserving; migrations are rewritten **in place** (not appended) since the application is pre-production and the MigrationRunner tracks applied scripts by filename — a fresh Postgres instance re-runs them cleanly.
- A local Postgres instance is available at a connection string the developer controls (mirrors the current assumption of `localhost\sql` for SQL Server). Testcontainers is explicitly out of scope — introducing it would be a separate decision per CLAUDE.md.
- Developers running locally will configure `dotnet user-secrets` (for the CLI) and an environment variable (for tests) before the first run. The plan documents the exact commands.

---

## File Structure

Files this plan touches:

- **Modify:** `src/AssetTracker.Core/AssetTracker.Core.csproj` — swap NuGet package
- **Modify:** `src/AssetTracker.Core/Database/ConnectionFactory.cs` — `SqlConnection` → `NpgsqlConnection`
- **Modify:** `src/AssetTracker.Core/Database/MigrationRunner.cs` — rewrite bootstrap SQL for Postgres
- **Modify:** `src/AssetTracker.Core/Repositories/AssetRepository.cs` — rewrite all queries
- **Modify:** `src/AssetTracker.Core/Domain/Asset.cs` — `CreatedAt` type change
- **Modify:** `src/AssetTracker.Core/Domain/BalanceEntry.cs` — `RecordedAt` type change
- **Modify:** `migrations/001_CreateAssets.sql` — Postgres DDL, snake_case
- **Modify:** `migrations/002_CreateBalanceEntries.sql` — Postgres DDL, snake_case
- **Create:** `src/AssetTracker.Core/Database/DapperConfig.cs` — one-time type map setup
- **Modify:** `src/AssetTracker.Cli/AssetTracker.Cli.csproj` — add UserSecrets package + `UserSecretsId`
- **Modify:** `src/AssetTracker.Cli/Program.cs` — call `DapperConfig.Configure()`, add `.AddUserSecrets()`
- **Modify:** `src/AssetTracker.Cli/appsettings.json` — remove `ConnectionStrings` (credentials move to user-secrets)
- **Modify:** `tests/AssetTracker.Tests/AssetTracker.Tests.csproj` — swap test project's SqlClient ref for Npgsql
- **Modify:** `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/TestConnectionFactory.cs` — read from env var (no hardcoded credentials)
- **Modify:** `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs` — rewrite inline SQL in tests (snake_case, `NOW()`, `FALSE`)
- **Modify:** `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs` — rewrite test migration DDL and `INFORMATION_SCHEMA` query

---

## Task 1: Swap NuGet packages and wire up user-secrets

**Files:**
- Modify: `src/AssetTracker.Core/AssetTracker.Core.csproj`
- Modify: `src/AssetTracker.Cli/AssetTracker.Cli.csproj`
- Modify: `tests/AssetTracker.Tests/AssetTracker.Tests.csproj`

- [ ] **Step 1: Remove SqlClient, add Npgsql to Core**

Edit `src/AssetTracker.Core/AssetTracker.Core.csproj`, replace:

```xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.0" />
```

with:

```xml
<PackageReference Include="Npgsql" Version="8.0.4" />
```

- [ ] **Step 2: Remove SqlClient, add Npgsql to Tests**

Edit `tests/AssetTracker.Tests/AssetTracker.Tests.csproj`, replace:

```xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.0" />
```

with:

```xml
<PackageReference Include="Npgsql" Version="8.0.4" />
```

- [ ] **Step 2b: Add user-secrets support to the CLI project**

Run (from repo root):

```bash
dotnet user-secrets init --project src/AssetTracker.Cli
```

This adds a `<UserSecretsId>...</UserSecretsId>` element to `src/AssetTracker.Cli/AssetTracker.Cli.csproj`. Commit that element — it's a project-level identifier, not a secret.

Then add the package reference by editing `src/AssetTracker.Cli/AssetTracker.Cli.csproj`. Inside the existing `<ItemGroup>` that holds `PackageReference` entries, add:

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="10.0.5" />
```

(Match the version used by the other `Microsoft.Extensions.Configuration.*` packages in that project.)

- [ ] **Step 3: Restore**

Run: `dotnet restore`
Expected: Restore completes cleanly; `Microsoft.Data.SqlClient` is gone from the lock file.

- [ ] **Step 4: Confirm build fails (expected — code still references SqlConnection)**

Run: `dotnet build`
Expected: FAIL. Error references `SqlConnection` in `ConnectionFactory.cs`. This confirms we need Task 2.

(No commit yet — broken intermediate state.)

---

## Task 2: Update ConnectionFactory to use Npgsql

**Files:**
- Modify: `src/AssetTracker.Core/Database/ConnectionFactory.cs`

- [ ] **Step 1: Replace `SqlConnection` with `NpgsqlConnection`**

Replace the entire file with:

```csharp
using System.Data;
using Npgsql;

namespace AssetTracker.Core.Database
{
    public class ConnectionFactory(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        // Returns IDbConnection, not NpgsqlConnection.
        // This keeps callers decoupled from the provider.
        public IDbConnection Create()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
```

- [ ] **Step 2: Verify build now compiles (tests will still fail at runtime)**

Run: `dotnet build`
Expected: PASS. The app compiles. Tests will fail at execution because migrations/repo SQL is still T-SQL, but compilation works.

- [ ] **Step 3: Commit**

```bash
git add src/AssetTracker.Core/AssetTracker.Core.csproj tests/AssetTracker.Tests/AssetTracker.Tests.csproj src/AssetTracker.Core/Database/ConnectionFactory.cs
git commit -m "refactor: replace Microsoft.Data.SqlClient with Npgsql driver"
```

---

## Task 3: Rewrite migration scripts for PostgreSQL

**Files:**
- Modify: `migrations/001_CreateAssets.sql`
- Modify: `migrations/002_CreateBalanceEntries.sql`

- [ ] **Step 1: Rewrite `001_CreateAssets.sql`**

Replace the entire file with:

```sql
CREATE TABLE assets
(
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(100) NOT NULL,
    category    VARCHAR(50)  NOT NULL,
    description VARCHAR(255) NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE
);
```

Notes on each conversion (for reviewer context, not for the file):
- `INT IDENTITY` → `SERIAL` (auto-incrementing int, Postgres idiom)
- `NVARCHAR` → `VARCHAR` (Postgres is Unicode natively; no N-prefix needed)
- `BIT` → `BOOLEAN`
- `DATETIME2` → `TIMESTAMPTZ` (timezone-aware, per your decision)
- `SYSUTCDATETIME()` → `NOW() AT TIME ZONE 'UTC'` (explicit UTC; `NOW()` alone returns local-server time)

- [ ] **Step 2: Rewrite `002_CreateBalanceEntries.sql`**

Replace the entire file with:

```sql
CREATE TABLE balance_entries
(
    id          SERIAL PRIMARY KEY,
    asset_id    INT            NOT NULL REFERENCES assets(id),
    balance     DECIMAL(18, 2) NOT NULL,
    recorded_at TIMESTAMPTZ    NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    note        VARCHAR(255)   NULL
);

CREATE INDEX ix_balance_entries_asset_id_recorded_at
    ON balance_entries (asset_id, recorded_at DESC);
```

- [ ] **Step 3: Commit (build still passes; tests still broken)**

```bash
git add migrations/001_CreateAssets.sql migrations/002_CreateBalanceEntries.sql
git commit -m "refactor: rewrite migrations as PostgreSQL DDL with snake_case and TIMESTAMPTZ"
```

---

## Task 4: Rewrite MigrationRunner bootstrap SQL

**Files:**
- Modify: `src/AssetTracker.Core/Database/MigrationRunner.cs:22-39`

- [ ] **Step 1: Replace `EnsureMigrationsTableAsync` and `GetAppliedMigrationsAsync`**

Replace those two methods with:

```csharp
private static async Task EnsureMigrationsTableAsync(IDbConnection conn)
{
    // CREATE TABLE IF NOT EXISTS is the idiomatic Postgres equivalent
    // of the OBJECT_ID check we had under SQL Server.
    await conn.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS __migrations (
            id          SERIAL PRIMARY KEY,
            file_name   VARCHAR(255) NOT NULL,
            applied_at  TIMESTAMPTZ  NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
        )
    ");
}

private static async Task<IEnumerable<string>> GetAppliedMigrationsAsync(IDbConnection conn)
{
    return await conn.QueryAsync<string>("SELECT file_name FROM __migrations");
}
```

- [ ] **Step 2: Update the INSERT in `ApplyMigrationAsync`**

In `MigrationRunner.cs:57-60`, change the INSERT column name from `FileName` to `file_name`:

```csharp
await conn.ExecuteAsync(
    "INSERT INTO __migrations (file_name) VALUES (@FileName)",
    new { FileName = fileName }
);
```

(The Dapper parameter `@FileName` stays — it maps by anonymous-object property name, not column name.)

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AssetTracker.Core/Database/MigrationRunner.cs
git commit -m "refactor: rewrite MigrationRunner bootstrap SQL for PostgreSQL"
```

---

## Task 5: Add Dapper snake_case ↔ PascalCase type mapping

**Files:**
- Create: `src/AssetTracker.Core/Database/DapperConfig.cs`
- Modify: `src/AssetTracker.Cli/Program.cs`
- Modify: `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs` (add setup call)
- Modify: `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs` (add setup call)

**Why this matters:** Postgres returns columns as `asset_id`, `created_at`. Our C# POCOs expose `AssetId`, `CreatedAt`. Dapper maps column→property by name, so without a bridge it would leave those properties null. Dapper ships `DefaultTypeMap.MatchNamesWithUnderscores` exactly for this.

**Note on uncertainty:** If `DefaultTypeMap.MatchNamesWithUnderscores` is not present in the installed Dapper version (2.1.72 should have it, but verify), fall back to registering a `CustomPropertyTypeMap` per POCO. The fallback is sketched at the end of this task.

- [ ] **Step 1: Create `DapperConfig.cs`**

Write `src/AssetTracker.Core/Database/DapperConfig.cs`:

```csharp
using Dapper;

namespace AssetTracker.Core.Database;

public static class DapperConfig
{
    private static bool _configured;

    /// <summary>
    /// Configures Dapper to map snake_case database columns to PascalCase
    /// C# properties (e.g., asset_id → AssetId). Must be called once at
    /// application startup before any Dapper query.
    /// </summary>
    public static void Configure()
    {
        if (_configured) return;
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        _configured = true;
    }
}
```

- [ ] **Step 2: Call it at CLI startup**

Edit `src/AssetTracker.Cli/Program.cs` — after the `using` block (before the `var config = ...` line), add:

```csharp
AssetTracker.Core.Database.DapperConfig.Configure();
```

- [ ] **Step 3: Call it in test fixtures**

Add a `[OneTimeSetUp]` method to both test fixtures so Dapper is configured before any query runs.

In `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs`, add:

```csharp
[OneTimeSetUp]
public void ConfigureDapper() => AssetTracker.Core.Database.DapperConfig.Configure();
```

Repeat in `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs`.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: PASS. If compilation fails on `DefaultTypeMap.MatchNamesWithUnderscores`, apply the fallback below.

- [ ] **Fallback (only if Step 4 fails):** Replace the body of `Configure()` with per-type maps:

```csharp
SqlMapper.SetTypeMap(typeof(Domain.Asset), BuildMap<Domain.Asset>());
SqlMapper.SetTypeMap(typeof(Domain.BalanceEntry), BuildMap<Domain.BalanceEntry>());

static CustomPropertyTypeMap BuildMap<T>() => new(
    typeof(T),
    (type, columnName) =>
    {
        var pascal = string.Concat(columnName.Split('_')
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
        return type.GetProperty(pascal)!;
    });
```

- [ ] **Step 5: Commit**

```bash
git add src/AssetTracker.Core/Database/DapperConfig.cs src/AssetTracker.Cli/Program.cs tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs
git commit -m "feat: add Dapper snake_case to PascalCase column mapping"
```

---

## Task 6: Rewrite AssetRepository queries

**Files:**
- Modify: `src/AssetTracker.Core/Repositories/AssetRepository.cs`

- [ ] **Step 1: Rewrite `GetAllActiveAsync`** (lines 12-22)

```csharp
public async Task<IEnumerable<Asset>> GetAllActiveAsync()
{
    using var conn = _factory.Create();

    return await conn.QueryAsync<Asset>(@"
        SELECT id, name, category, description, created_at, is_active
        FROM assets
        WHERE is_active = TRUE
        ORDER BY name
    ");
}
```

- [ ] **Step 2: Rewrite `GetAllActiveWithLatestBalanceAsync`** (lines 24-59)

```csharp
public async Task<IEnumerable<Asset>> GetAllActiveWithLatestBalanceAsync()
{
    using var conn = _factory.Create();

    var sql = @"
        WITH latest_balances AS (
            SELECT
                id,
                asset_id,
                balance,
                recorded_at,
                note,
                ROW_NUMBER() OVER (PARTITION BY asset_id ORDER BY recorded_at DESC) AS rn
            FROM balance_entries
        )
        SELECT
            a.id, a.name, a.category, a.description, a.created_at, a.is_active,
            lb.id, lb.asset_id, lb.balance, lb.recorded_at, lb.note
        FROM assets a
        LEFT JOIN latest_balances lb ON a.id = lb.asset_id AND lb.rn = 1
        WHERE a.is_active = TRUE
        ORDER BY a.name
    ";

    var assets = await conn.QueryAsync<Asset, BalanceEntry, Asset>(
        sql,
        (asset, balance) =>
        {
            asset.LatestBalance = balance;
            return asset;
        },
        splitOn: "id"
    );

    return assets;
}
```

Note the `splitOn` column name changed from `"Id"` to `"id"` since Postgres returns lowercase column names.

- [ ] **Step 3: Rewrite `GetByIdAsync`** (lines 61-73)

```csharp
public async Task<Asset?> GetByIdAsync(int id)
{
    using var conn = _factory.Create();

    var p = new DynamicParameters();
    p.Add("Id", id, DbType.Int32);

    return await conn.QuerySingleOrDefaultAsync<Asset>(@"
        SELECT id, name, category, description, created_at, is_active
        FROM assets
        WHERE id = @Id
    ", p);
}
```

- [ ] **Step 4: Rewrite `AddAsync`** (lines 75-89) — `OUTPUT INSERTED.Id` → `RETURNING id`

```csharp
public async Task<int> AddAsync(string name, string category, string? description)
{
    using var conn = _factory.Create();

    var p = new DynamicParameters();
    p.Add("Name", name, DbType.String, size: 100);
    p.Add("Category", category, DbType.String, size: 50);
    p.Add("Description", description, DbType.String, size: 255);

    return await conn.ExecuteScalarAsync<int>(@"
        INSERT INTO assets (name, category, description)
        VALUES (@Name, @Category, @Description)
        RETURNING id
    ", p);
}
```

- [ ] **Step 5: Rewrite `AddWithInitialBalanceAsync`** (lines 91-117)

```csharp
public async Task<int> AddWithInitialBalanceAsync(string name, string category, string? description, decimal initialBalance) {
    using var conn = _factory.Create();
    conn.Open();

    using var transaction = conn.BeginTransaction();

    try {
        var assetId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO assets (name, category, description)
            VALUES (@Name, @Category, @Description)
            RETURNING id
        ", new { Name = name, Category = category, Description = description }, transaction: transaction);

        await conn.ExecuteAsync(@"
            INSERT INTO balance_entries (asset_id, balance, note)
            VALUES (@AssetId, @Balance, @Note)
        ", new { AssetId = assetId, Balance = initialBalance, Note = description ?? "Initial balance" }, transaction: transaction);

        transaction.Commit();
        return assetId;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

- [ ] **Step 6: Rewrite `RecordBalanceAsync`** (lines 119-132)

```csharp
public async Task RecordBalanceAsync(int assetId, decimal balance, string? note)
{
    using var conn = _factory.Create();

    var p = new DynamicParameters();
    p.Add("AssetId", assetId, DbType.Int32);
    p.Add("Balance", balance, DbType.Decimal);
    p.Add("Note", note, DbType.String, size: 255);

    await conn.ExecuteAsync(@"
        INSERT INTO balance_entries (asset_id, balance, note)
        VALUES (@AssetId, @Balance, @Note)
    ", p);
}
```

- [ ] **Step 7: Rewrite `GetBalanceHistoryAsync`** (lines 134-147)

```csharp
public async Task<IEnumerable<BalanceEntry>> GetBalanceHistoryAsync(int assetId)
{
    using var conn = _factory.Create();

    var p = new DynamicParameters();
    p.Add("AssetId", assetId, DbType.Int32);

    return await conn.QueryAsync<BalanceEntry>(@"
        SELECT id, asset_id, balance, recorded_at, note
        FROM balance_entries
        WHERE asset_id = @AssetId
        ORDER BY recorded_at DESC
    ", p);
}
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AssetTracker.Core/Repositories/AssetRepository.cs
git commit -m "refactor: rewrite AssetRepository queries for PostgreSQL (snake_case + RETURNING)"
```

---

## Task 7: Promote datetime properties to `DateTimeOffset`

**Files:**
- Modify: `src/AssetTracker.Core/Domain/Asset.cs`
- Modify: `src/AssetTracker.Core/Domain/BalanceEntry.cs`

**Why this matters:** We're now storing timezone-aware timestamps (`TIMESTAMPTZ`). A `DateTime` property can represent either UTC or local time depending on its `Kind`, which makes bugs easy to introduce. `DateTimeOffset` forces the caller to think about offset at construction time, and Npgsql maps it to/from `TIMESTAMPTZ` cleanly. The display format strings used in [Commands.cs:96](src/AssetTracker.Cli/Commands.cs#L96) (`yyyy-MM-dd HH:mm`) work identically on both types, so the consumer code doesn't need to change.

- [ ] **Step 1: Update `Asset.cs`**

In `src/AssetTracker.Core/Domain/Asset.cs`, change:
```csharp
public DateTime CreatedAt { get; set; }
```
to:
```csharp
public DateTimeOffset CreatedAt { get; set; }
```

- [ ] **Step 2: Update `BalanceEntry.cs`**

In `src/AssetTracker.Core/Domain/BalanceEntry.cs`, change:
```csharp
public DateTime RecordedAt { get; set; }
```
to:
```csharp
public DateTimeOffset RecordedAt { get; set; }
```

- [ ] **Step 3: Build to verify no caller broke**

Run: `dotnet build`
Expected: PASS. The display format strings in [Commands.cs](src/AssetTracker.Cli/Commands.cs) accept both types. Dapper converts `TIMESTAMPTZ` values to `DateTimeOffset` automatically via Npgsql's built-in type handler.

- [ ] **Step 4: Commit**

```bash
git add src/AssetTracker.Core/Domain/Asset.cs src/AssetTracker.Core/Domain/BalanceEntry.cs
git commit -m "refactor: use DateTimeOffset for domain timestamps to match TIMESTAMPTZ semantics"
```

---

## Task 8: Update test infrastructure

**Files:**
- Modify: `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/TestConnectionFactory.cs`
- Modify: `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs`
- Modify: `tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs`

- [ ] **Step 1: Point `TestConnectionFactory` at Postgres via environment variable**

Replace the file with:

```csharp
using AssetTracker.Core.Database;

namespace AssetTracker.Tests;

public static class TestConnectionFactory
{
    private const string EnvVarName = "ASSETTRACKER_TEST_CONNECTION_STRING";

    public static ConnectionFactory Create()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException(
                $"Environment variable '{EnvVarName}' is not set. " +
                "Example: Host=localhost;Database=asset_tracker_test;Username=postgres;Password=***");
        return new ConnectionFactory(connectionString);
    }
}
```

**Rationale:** No credential ever lives in a committed file. If a developer forgets to set the env var, they get an actionable error message pointing them at the variable name and example format. For CI, set the env var in the pipeline's secrets.

Set it locally (one-time per shell, or permanently via your OS user environment):

```bash
# Git Bash / WSL
export ASSETTRACKER_TEST_CONNECTION_STRING="Host=localhost;Database=asset_tracker_test;Username=postgres;Password=<your-password>"

# PowerShell (persistent, current user)
[Environment]::SetEnvironmentVariable("ASSETTRACKER_TEST_CONNECTION_STRING", "Host=localhost;Database=asset_tracker_test;Username=postgres;Password=<your-password>", "User")
```

This test DB should already exist and be empty.

- [ ] **Step 2: Fix inline SQL in `AssetRepositoryTests.cs`**

Replace the two inline SQL statements in the test file:

In `[SetUp]` (line 19), change:
```csharp
await conn.ExecuteAsync("DELETE FROM BalanceEntries; DELETE From Assets;");
```
to:
```csharp
await conn.ExecuteAsync("DELETE FROM balance_entries; DELETE FROM assets;");
```

In `GetAllActiveAsync_ShouldReturnOnlyActiveAssets` (line 36), change:
```csharp
await conn.ExecuteAsync("INSERT INTO Assets (Name, Category, Description, CreatedAt, IsActive) VALUES ('Inactive', 'Checking', NULL, GETDATE(), 0);");
```
to:
```csharp
await conn.ExecuteAsync("INSERT INTO assets (name, category, description, created_at, is_active) VALUES ('Inactive', 'Checking', NULL, NOW(), FALSE);");
```

- [ ] **Step 3: Fix test migration DDL and table-exists query in `MigrationRunnerTests.cs`**

Change the test migration DDL (line 44-49) from:
```csharp
await File.WriteAllTextAsync(scriptPath, @"
    CREATE TABLE TestAssets (
        Id   INT IDENTITY PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL
    );
");
```
to:
```csharp
await File.WriteAllTextAsync(scriptPath, @"
    CREATE TABLE test_assets (
        id   SERIAL PRIMARY KEY,
        name VARCHAR(100) NOT NULL
    );
");
```

Change the teardown (line 27-30) from:
```csharp
await conn.ExecuteAsync(@"
    DROP TABLE IF EXISTS TestAssets;
    DROP TABLE IF EXISTS __Migrations;
");
```
to:
```csharp
await conn.ExecuteAsync(@"
    DROP TABLE IF EXISTS test_assets;
    DROP TABLE IF EXISTS __migrations;
");
```

Change the `SELECT FileName` query (line 62) from:
```csharp
var appliedMigrations = await conn.QueryAsync<string>(
    "SELECT FileName FROM __Migrations"
);
```
to:
```csharp
var appliedMigrations = await conn.QueryAsync<string>(
    "SELECT file_name FROM __migrations"
);
```

Change the INFORMATION_SCHEMA query (line 69-73) — Postgres folds unquoted table names to lowercase, so the `TABLE_NAME` comparison must match lowercase:
```csharp
var tableExists = await conn.ExecuteScalarAsync<int>(@"
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'test_assets'
");
```

(`INFORMATION_SCHEMA` itself is standardized and works in both engines.)

- [ ] **Step 4: Ensure local Postgres has `asset_tracker_test` database and env var is set**

Run (adjust for your Postgres setup):
```bash
psql -U postgres -c "CREATE DATABASE asset_tracker_test;"
```

Expected: Succeeds, or fails with "database already exists" (both are fine).

Verify the env var is exported in your current shell:
```bash
echo $ASSETTRACKER_TEST_CONNECTION_STRING    # Git Bash / WSL
$env:ASSETTRACKER_TEST_CONNECTION_STRING     # PowerShell
```
Expected: Prints your connection string. If it prints empty, `dotnet test` will throw from `TestConnectionFactory.Create()` with the message you wrote in Step 1.

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: **All tests PASS.** This is the integration check that proves the port works end-to-end against Postgres.

If any test fails, debug the specific failure — do not move on. Common issues:
- Connection refused: local Postgres isn't running
- `relation "assets" does not exist`: migrations didn't run (check the MigrationRunner test ran first, or manually apply migrations to the test DB)
- Property not populated on POCO: `DapperConfig.Configure()` didn't fire before the query (check `[OneTimeSetUp]` is present)

- [ ] **Step 6: Commit**

```bash
git add tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/TestConnectionFactory.cs tests/AssetTracker.Tests/AssetTracker.Core.Tests/Repositories/AssetRepositoryTests.cs tests/AssetTracker.Tests/AssetTracker.Core.Tests/Database/MigrationRunnerTests.cs
git commit -m "test: point test suite at PostgreSQL via env-var connection string"
```

---

## Task 9: Update CLI configuration (remove credentials, wire up user-secrets)

**Files:**
- Modify: `src/AssetTracker.Cli/appsettings.json`
- Modify: `src/AssetTracker.Cli/Program.cs`

- [ ] **Step 1: Remove `ConnectionStrings` from committed `appsettings.json`**

Replace the file with:

```json
{
  "Paths": {
    "Migrations": "C:\\Users\\ahebert\\source\\github\\asset-tracker\\migrations"
  }
}
```

No committed file now contains any piece of a connection string. The app's existing guard at [Program.cs:12-13](src/AssetTracker.Cli/Program.cs#L12-L13) (`?? throw new InvalidOperationException(...)`) will fire with a clear message if the developer hasn't set up user-secrets.

- [ ] **Step 2: Wire up `.AddUserSecrets<Program>()` in `Program.cs`**

At the top of `src/AssetTracker.Cli/Program.cs`, edit the `ConfigurationBuilder` chain (lines 6-10) to:

```csharp
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true) // gitignored overrides
    .AddUserSecrets<Program>()                              // per-developer secrets, outside the repo
    .Build();
```

Note: `.AddUserSecrets<Program>()` requires `Program` to be a named type. With top-level statements the compiler generates an internal `Program` class automatically — this works. If the compiler complains (older SDKs), swap to `.AddUserSecrets(Assembly.GetExecutingAssembly())`.

Also add `DapperConfig.Configure()` earlier in the file — this was already planned in Task 5 Step 2 and should be in place by now. Verify.

- [ ] **Step 3: Set the CLI connection string via user-secrets**

Run (from repo root):

```bash
dotnet user-secrets set "ConnectionStrings:AssetTracker" "Host=localhost;Database=asset_tracker;Username=postgres;Password=<your-password>" --project src/AssetTracker.Cli
```

Expected: `Successfully saved ConnectionStrings:AssetTracker = ...` to a file in `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`. That file is outside the repo tree and cannot be accidentally committed.

- [ ] **Step 4: Ensure local Postgres has `asset_tracker` database**

Run:
```bash
psql -U postgres -c "CREATE DATABASE asset_tracker;"
```

- [ ] **Step 5: Run the CLI end-to-end**

Run: `dotnet run --project src/AssetTracker.Cli`

Then at the prompt, exercise the main paths:
```
> add Checking|Cash
> addb Savings|Cash|Rainy day fund|1000.00
> update 2|1500|Deposit
> summary
> history 2
> quit
```

Expected:
- Migrations run on first launch (watch for no errors).
- Each command succeeds and returns sensible output.
- `summary` shows the latest balance per asset (tests the window-function CTE).
- `history` shows entries in reverse chronological order.

- [ ] **Step 6: Commit**

```bash
git add src/AssetTracker.Cli/appsettings.json src/AssetTracker.Cli/Program.cs src/AssetTracker.Cli/AssetTracker.Cli.csproj
git commit -m "chore: move CLI connection string to user-secrets, remove credentials from repo"
```

(The `.csproj` change for `UserSecretsId` was introduced in Task 1 Step 2b, but may not have been committed yet — this is the natural commit for it to land with.)

---

## Task 10: Final verification

- [ ] **Step 1: Clean build**

Run: `dotnet build --no-incremental`
Expected: PASS with zero warnings related to the migration.

- [ ] **Step 2: Full test run**

Run: `dotnet test`
Expected: All tests PASS.

- [ ] **Step 3: Confirm no SQL Server references remain**

Run: `grep -r -i "sqlclient\|sqlconnection\|datetime2\|nvarchar\|sysutcdatetime\|OBJECT_ID\|OUTPUT INSERTED" src tests migrations --include="*.cs" --include="*.sql" --include="*.csproj"`
Expected: No matches. If any appear, investigate whether they should be updated or are genuinely unrelated.

- [ ] **Step 4: Confirm no credentials are committed**

Run: `grep -r -n -i "password" src tests migrations --include="*.cs" --include="*.sql" --include="*.json" --include="*.csproj"`
Expected: No matches. If any appear (including in comments or connection string placeholders), remove them. The only place a password should exist is in `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` (user-secrets, outside the repo) and in developer/CI environment variables.

- [ ] **Step 5: Sanity check the migrations table state**

Run:
```bash
psql -U postgres -d asset_tracker -c "SELECT file_name, applied_at FROM __migrations ORDER BY id;"
```
Expected: Two rows — `001_CreateAssets.sql` and `002_CreateBalanceEntries.sql`, each with a recent `applied_at`.

---

## Out of scope / follow-ups worth considering

These are deliberately NOT in this plan — call them out in review if you want them addressed separately:

- **Testcontainers** for repository/migration tests. Would eliminate the local-Postgres assumption but introduces a new dependency (needs explicit approval per CLAUDE.md).
- **Connection pooling / `NpgsqlDataSource`**: Npgsql 7+ recommends `NpgsqlDataSource` as a singleton instead of constructing connections from strings. Worth adopting eventually; out of scope for a straight port.
- **Migration reversibility**: Neither the old nor the new migrations support rollback. Still out of scope.
- **A README / setup doc** capturing the one-time developer setup (install Postgres, create dev + test DBs, `dotnet user-secrets set ...`, set `ASSETTRACKER_TEST_CONNECTION_STRING`). Worth adding but doesn't belong in the same PR as the port.
