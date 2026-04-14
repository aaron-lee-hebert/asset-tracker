# Learning Exercises

Practice tasks organized by skill. Each builds on the existing codebase.
Difficulty: [1] warmup, [2] moderate, [3] challenging.

---

## PostgreSQL

- [ ] **[1] Write migration 003: add a `categories` table and normalize**
  Extract `category` from a free-text column on `assets` into its own table
  with an `id` + `name`. Add a foreign key `assets.category_id` referencing it.
  _Teaches: ALTER TABLE, data migration with INSERT...SELECT DISTINCT, foreign keys._

- [ ] **[1] Add a CHECK constraint to `balance_entries`**
  Write migration 004 that adds `CHECK (balance >= 0)` if your domain forbids
  negative balances, or `CHECK (LENGTH(note) <= 255)` as a DB-level guard.
  _Teaches: CHECK constraints, when DB-level validation complements app-level._

- [ ] **[2] Write a net-worth-by-category query using GROUP BY + window functions**
  `SELECT category, SUM(latest_balance) ...` — reuse the `ROW_NUMBER()` CTE
  pattern from `GetAllActiveWithLatestBalanceAsync` but add a `GROUP BY` layer.
  _Teaches: aggregate functions, nesting CTEs, GROUP BY with window functions._

- [ ] **[2] Add a date-range filter to balance history**
  `GetBalanceHistoryAsync(int assetId, DateTimeOffset? from, DateTimeOffset? to)`
  — build the WHERE clause conditionally with Dapper `DynamicParameters`.
  _Teaches: optional parameters in SQL, TIMESTAMPTZ range comparisons, conditional WHERE clauses._

- [ ] **[3] Write a balance-trend query that computes period-over-period change**
  Use `LAG(balance) OVER (PARTITION BY asset_id ORDER BY recorded_at)` to show
  the delta between consecutive balance entries.
  _Teaches: LAG/LEAD window functions, computed columns in SELECT._

---

## Dapper

- [ ] **[1] Use `QueryMultipleAsync` to fetch an asset and its history in one round-trip**
  Execute two SELECTs in a single command string, then call `reader.ReadAsync<Asset>()`
  and `reader.ReadAsync<BalanceEntry>()` to hydrate both from one DB call.
  _Teaches: QueryMultiple, reducing round trips, result-set sequencing._

- [ ] **[2] Build a bulk-insert for balance entries using Dapper's list-parameter expansion**
  `conn.ExecuteAsync("INSERT INTO balance_entries ...", listOfEntries)` — Dapper
  unrolls the list into individual parameterized inserts inside one command.
  _Teaches: Dapper collection parameter expansion, batch operations._

- [ ] **[2] Implement a search method with dynamic WHERE clauses**
  `SearchAssetsAsync(string? name, string? category, bool? isActive)` — build
  SQL conditionally using `SqlBuilder` or string concatenation with `DynamicParameters`.
  _Teaches: dynamic query building, avoiding SQL injection with parameterized dynamic SQL._

- [ ] **[3] Create a custom Dapper `SqlMapper.TypeHandler<T>` for a `Money` value object**
  Wrap `decimal` in a `readonly record struct Money(decimal Amount)`. Write a
  type handler so Dapper maps `DECIMAL(18,2)` columns to `Money` transparently.
  _Teaches: Dapper extensibility, value objects, custom type mapping._

---

## NUnit

- [ ] **[1] Add tests for the three untested repository methods**
  `GetByIdAsync`, `AddWithInitialBalanceAsync`, and `GetAllActiveWithLatestBalanceAsync`
  each have zero test coverage. Write at least one happy-path test per method.
  _Teaches: integration testing against a real DB, Dapper result verification._

- [ ] **[1] Test edge cases: null description, zero balance, duplicate names**
  What happens when `AddAsync("Savings", "Cash", null)` is called twice?
  What about `RecordBalanceAsync(id, 0m, null)`? Write tests that document
  the actual behavior — even if it's "it works fine."
  _Teaches: boundary testing, documenting implicit behavior via tests._

- [ ] **[2] Use `[TestCase]` or `[TestCaseSource]` for parameterized balance tests**
  Refactor `RecordBalanceAsync_ShouldAppendEntry` into a parameterized test
  that covers multiple scenarios: one entry, many entries, zero-amount, large decimals.
  _Teaches: NUnit parameterized tests, reducing test duplication._

- [ ] **[2] Test that `AddWithInitialBalanceAsync` rolls back on failure**
  Force a failure after the asset INSERT but before the balance INSERT (e.g., by
  passing a value that violates a constraint). Assert that no asset was created.
  _Teaches: testing transactional behavior, verifying rollback actually works._

- [ ] **[3] Test migration idempotency**
  Run `MigrationRunner.RunAsync()` twice on the same database. Assert that the
  second run applies zero new migrations and doesn't error.
  _Teaches: idempotency verification, integration testing infrastructure code._

---

## C# / Architecture

- [ ] **[1] Add a `deactivate` command that soft-deletes an asset**
  New method `DeactivateAsync(int id)` — `UPDATE assets SET is_active = FALSE WHERE id = @Id`.
  Wire it into `Commands.cs` as the `deactivate <id>` command.
  _Teaches: UPDATE queries, extending the CLI dispatch, soft-delete pattern._

- [ ] **[1] Add a `help` command with per-command usage**
  `help` prints all commands; `help add` prints the usage for `add` specifically.
  Store usage strings in a dictionary keyed by command name.
  _Teaches: simple dispatch patterns, Dictionary<string, string>, self-documenting CLIs._

- [ ] **[2] Extract command dispatch into a command-object pattern**
  Replace the `switch` in `Program.cs` with an `ICommand` interface
  (`string Name`, `string Usage`, `Task ExecuteAsync(repo, args)`). Register
  commands in a list and dispatch by name lookup.
  _Teaches: interface design, open/closed principle, replacing conditionals with polymorphism._

- [ ] **[2] Add input validation with a `ValidationException`**
  Throw `ValidationException("Balance must be positive")` from repository methods
  when invariants are violated. Catch it in the CLI loop and print a friendly message.
  _Teaches: custom exceptions, validation boundaries, separation of concerns._

- [ ] **[3] Make `ConnectionFactory` implement `IAsyncDisposable` with `NpgsqlDataSource`**
  Npgsql recommends a singleton `NpgsqlDataSource` over constructing connections
  from strings. Refactor `ConnectionFactory` to wrap a `NpgsqlDataSource`, and
  dispose it on app shutdown via `await using`.
  _Teaches: IAsyncDisposable, Npgsql best practices, singleton lifecycle._

---

## Stretch Goals

- [ ] **[3] Add a second project: `AssetTracker.Api` (minimal API)**
  Expose the same `IAssetRepository` over HTTP endpoints. Reuse the Core
  project — the repository and domain types are already provider-agnostic.
  _Teaches: minimal APIs, dependency injection, reusing a shared Core library._

- [ ] **[3] Add Testcontainers so tests spin up a disposable Postgres automatically**
  Replace the env-var connection string with `Testcontainers.PostgreSql`.
  Each test run gets a fresh, ephemeral database — no manual DB setup required.
  _Teaches: Testcontainers, Docker-based test infrastructure, CI-friendly tests._
