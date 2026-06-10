using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Npgsql;

var options = BenchmarkOptions.Parse(args);
if (options.ShowHelp || !options.HasAction)
{
    BenchmarkOptions.PrintHelp();
    return options.ShowHelp ? 0 : 1;
}

if (options.Measure && (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password)))
{
    Console.Error.WriteLine("Missing --email or --password. Measuring the protected endpoint requires SuperAdmin login.");
    return 1;
}

try
{
    if (options.Cleanup)
    {
        await CleanupAsync(options);
    }

    if (options.Seed)
    {
        await SeedAsync(options);
    }

    if (options.Measure)
    {
        return await MeasureAsync(options);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static async Task SeedAsync(BenchmarkOptions options)
{
    await using var connection = new NpgsqlConnection(options.ConnectionString);
    await connection.OpenAsync();
    await EnsureBenchmarkTablesExistAsync(connection);

    var existingRows = await CountBenchmarkRowsAsync(connection);
    Console.WriteLine($"Benchmark rows currently in ExportLogs: {existingRows:N0}");
    Console.WriteLine($"Target benchmark rows: {options.Rows:N0}");

    if (existingRows >= options.Rows)
    {
        Console.WriteLine("Seed skipped because the target row count already exists.");
        return;
    }

    var fromUtc = options.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toExclusiveUtc = options.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var rangeSeconds = Math.Max(1, (long)(toExclusiveUtc - fromUtc).TotalSeconds);
    var stopwatch = Stopwatch.StartNew();

    Console.WriteLine(
        $"Seeding {options.Rows:N0} deterministic rows across {options.From:yyyy-MM-dd}..{options.To:yyyy-MM-dd} " +
        $"with batch size {options.BatchSize:N0}.");

    for (var start = 1L; start <= options.Rows; start += options.BatchSize)
    {
        var end = Math.Min(options.Rows, start + options.BatchSize - 1);
        await SeedBatchAsync(connection, options.RunId, start, end, fromUtc, rangeSeconds, options.CommandTimeoutSeconds);
        Console.WriteLine($"Seeded through row {end:N0} / {options.Rows:N0} in {stopwatch.Elapsed}.");
    }

    await ExecuteNonQueryAsync(
        connection,
        """
        ANALYZE "ExportLogs";
        ANALYZE "ExportAnalyticsSnapshots";
        """,
        options.CommandTimeoutSeconds);

    var finalRows = await CountBenchmarkRowsAsync(connection);
    Console.WriteLine($"Seed complete. Benchmark rows now in ExportLogs: {finalRows:N0}. Elapsed: {stopwatch.Elapsed}.");
}

static async Task CleanupAsync(BenchmarkOptions options)
{
    await using var connection = new NpgsqlConnection(options.ConnectionString);
    await connection.OpenAsync();
    await EnsureBenchmarkTablesExistAsync(connection);

    var before = await CountBenchmarkRowsAsync(connection);
    Console.WriteLine($"Deleting {before:N0} benchmark ExportLogs rows. Related snapshots delete by cascade.");

    await ExecuteNonQueryAsync(
        connection,
        """
        DELETE FROM "ExportLogs"
        WHERE "UserId" LIKE 'analytics-benchmark-client-%';
        ANALYZE "ExportLogs";
        ANALYZE "ExportAnalyticsSnapshots";
        """,
        options.CommandTimeoutSeconds);

    var after = await CountBenchmarkRowsAsync(connection);
    Console.WriteLine($"Cleanup complete. Benchmark rows remaining: {after:N0}.");
}

static async Task<int> MeasureAsync(BenchmarkOptions options)
{
    var cookieContainer = new CookieContainer();
    using var handler = new HttpClientHandler
    {
        CookieContainer = cookieContainer,
        AllowAutoRedirect = false
    };
    using var client = new HttpClient(handler)
    {
        BaseAddress = options.BaseUrl
    };

    var loginResponse = await client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequest(options.Email!, options.Password!, RememberMe: false));
    if (!loginResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine(
            $"Login failed with {(int)loginResponse.StatusCode} {loginResponse.StatusCode}. " +
            "Use SuperAdmin credentials.");
        Console.Error.WriteLine(await loginResponse.Content.ReadAsStringAsync());
        return 1;
    }

    var query = $"/api/admin/analytics/business-demand?from={options.From:yyyy-MM-dd}&to={options.To:yyyy-MM-dd}";
    Console.WriteLine($"Measuring GET {query}");
    Console.WriteLine($"Runs: {options.Runs}, warmups: {options.Warmups}, target: {options.TargetMilliseconds:N0} ms");

    for (var warmup = 1; warmup <= options.Warmups; warmup++)
    {
        var warmupResult = await MeasureOnceAsync(client, query);
        Console.WriteLine(
            $"Warmup {warmup}: {(int)warmupResult.StatusCode} {warmupResult.StatusCode}, " +
            $"{warmupResult.Elapsed.TotalMilliseconds:N0} ms, {warmupResult.ResponseBytes:N0} bytes");
    }

    var results = new List<EndpointMeasurement>();
    for (var run = 1; run <= options.Runs; run++)
    {
        var result = await MeasureOnceAsync(client, query);
        results.Add(result);
        Console.WriteLine(
            $"Run {run}: {(int)result.StatusCode} {result.StatusCode}, " +
            $"{result.Elapsed.TotalMilliseconds:N0} ms, {result.ResponseBytes:N0} bytes");
    }

    var successfulResults = results.Where(result => result.StatusCode == HttpStatusCode.OK).ToArray();
    if (successfulResults.Length != results.Count)
    {
        Console.Error.WriteLine("One or more benchmark requests failed.");
        return 1;
    }

    var durations = successfulResults
        .Select(result => result.Elapsed.TotalMilliseconds)
        .OrderBy(value => value)
        .ToArray();
    var min = durations.First();
    var max = durations.Last();
    var average = durations.Average();

    Console.WriteLine("Summary:");
    Console.WriteLine($"  Min: {min:N0} ms");
    Console.WriteLine($"  Avg: {average:N0} ms");
    Console.WriteLine($"  Max: {max:N0} ms");

    if (max > options.TargetMilliseconds)
    {
        Console.Error.WriteLine(
            $"Target failed: max {max:N0} ms exceeded {options.TargetMilliseconds:N0} ms.");
        return 2;
    }

    Console.WriteLine($"Target passed: max {max:N0} ms <= {options.TargetMilliseconds:N0} ms.");
    return 0;
}

static async Task<EndpointMeasurement> MeasureOnceAsync(HttpClient client, string query)
{
    var stopwatch = Stopwatch.StartNew();
    using var response = await client.GetAsync(query);
    var bytes = await response.Content.ReadAsByteArrayAsync();
    stopwatch.Stop();

    return new EndpointMeasurement(response.StatusCode, stopwatch.Elapsed, bytes.Length);
}

static async Task EnsureBenchmarkTablesExistAsync(NpgsqlConnection connection)
{
    await using var command = new NpgsqlCommand(
        """
        SELECT to_regclass('"ExportLogs"') IS NOT NULL
           AND to_regclass('"ExportAnalyticsSnapshots"') IS NOT NULL;
        """,
        connection);
    var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
    if (!exists)
    {
        throw new InvalidOperationException(
            "ExportLogs or ExportAnalyticsSnapshots table was not found. Apply migrations before running this benchmark.");
    }
}

static async Task<long> CountBenchmarkRowsAsync(NpgsqlConnection connection)
{
    await using var command = new NpgsqlCommand(
        """
        SELECT count(*)
        FROM "ExportLogs"
        WHERE "UserId" LIKE 'analytics-benchmark-client-%';
        """,
        connection);
    return (long)(await command.ExecuteScalarAsync() ?? 0L);
}

static async Task SeedBatchAsync(
    NpgsqlConnection connection,
    string runId,
    long start,
    long end,
    DateTime fromUtc,
    long rangeSeconds,
    int commandTimeoutSeconds)
{
    await using var command = new NpgsqlCommand(BenchmarkSql.SeedBatch, connection)
    {
        CommandTimeout = commandTimeoutSeconds
    };
    command.Parameters.AddWithValue("runId", runId);
    command.Parameters.AddWithValue("startValue", start);
    command.Parameters.AddWithValue("endValue", end);
    command.Parameters.AddWithValue("fromUtc", fromUtc);
    command.Parameters.AddWithValue("rangeSeconds", rangeSeconds);
    await command.ExecuteNonQueryAsync();
}

static async Task ExecuteNonQueryAsync(
    NpgsqlConnection connection,
    string sql,
    int commandTimeoutSeconds)
{
    await using var command = new NpgsqlCommand(sql, connection)
    {
        CommandTimeout = commandTimeoutSeconds
    };
    await command.ExecuteNonQueryAsync();
}

file static class BenchmarkSql
{
    public const string SeedBatch =
        """
    WITH generated AS (
        SELECT
            g,
            (
                substr(md5(@runId || ':log:' || g::text), 1, 8) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 9, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 13, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 17, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 21, 12)
            )::uuid AS log_id,
            (
                substr(md5(@runId || ':snapshot:' || g::text), 1, 8) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 9, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 13, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 17, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 21, 12)
            )::uuid AS snapshot_id,
            @fromUtc::timestamptz + ((g % @rangeSeconds) * interval '1 second') AS timestamp_utc,
            ('analytics-benchmark-client-' || lpad((g % 1000)::text, 4, '0')) AS user_id,
            ('analytics-benchmark-client-' || lpad((g % 1000)::text, 4, '0') || '@example.test') AS user_email,
            (500 + (g % 12000))::integer AS requested_rows,
            CASE
                WHEN g % 23 = 0 THEN 0
                WHEN g % 7 = 0 THEN 1000
                ELSE LEAST((500 + (g % 12000))::integer, 5000)
            END AS exported_rows,
            (g % 7 = 0 AND g % 23 <> 0) AS was_truncated,
            CASE WHEN g % 23 = 0 THEN 'DailyExportOperationLimitReached' ELSE NULL END AS blocked_reason,
            CASE WHEN g % 5 = 0 THEN 'GoogleDrive' ELSE 'Download' END AS destination,
            CASE
                WHEN g % 25 = 0 THEN jsonb_build_object('schemaVersion', 1, 'filters', '[]'::jsonb)
                ELSE jsonb_build_object(
                    'schemaVersion', 1,
                    'filters', jsonb_build_array(
                        jsonb_build_object(
                            'field', 'locationKey',
                            'kind', 'multiSelect',
                            'operator', 'anyOf',
                            'value', to_jsonb(ARRAY[
                                CASE g % 6
                                    WHEN 0 THEN 'US'
                                    WHEN 1 THEN 'GB'
                                    WHEN 2 THEN 'DE'
                                    WHEN 3 THEN 'FR'
                                    WHEN 4 THEN 'CA'
                                    ELSE 'AU'
                                END
                            ]::text[])),
                        jsonb_build_object(
                            'field', 'niche',
                            'kind', 'multiSelect',
                            'operator', 'anyOf',
                            'value', to_jsonb(ARRAY[
                                CASE g % 5
                                    WHEN 0 THEN 'casino'
                                    WHEN 1 THEN 'crypto'
                                    WHEN 2 THEN 'finance'
                                    WHEN 3 THEN 'travel'
                                    ELSE 'technology'
                                END
                            ]::text[])),
                        jsonb_build_object(
                            'field', 'categories',
                            'kind', 'textSearch',
                            'operator', 'containsAny',
                            'value', to_jsonb(ARRAY[
                                CASE g % 5
                                    WHEN 0 THEN 'Finance'
                                    WHEN 1 THEN 'Business'
                                    WHEN 2 THEN 'News'
                                    WHEN 3 THEN 'Lifestyle'
                                    ELSE 'Technology'
                                END
                            ]::text[])),
                        jsonb_build_object(
                            'field', 'language',
                            'kind', 'multiSelect',
                            'operator', 'anyOf',
                            'value', to_jsonb(ARRAY[
                                CASE g % 4
                                    WHEN 0 THEN 'en'
                                    WHEN 1 THEN 'de'
                                    WHEN 2 THEN 'fr'
                                    ELSE 'es'
                                END
                            ]::text[])),
                        jsonb_build_object(
                            'field', 'priceCasinoAvailability',
                            'kind', 'availability',
                            'operator', 'in',
                            'value', CASE
                                WHEN g % 11 = 0 THEN to_jsonb(ARRAY['available', 'notAvailable']::text[])
                                WHEN g % 3 = 0 THEN to_jsonb(ARRAY['notAvailable']::text[])
                                ELSE to_jsonb(ARRAY['available']::text[])
                            END),
                        jsonb_build_object(
                            'field', 'dr',
                            'kind', 'numberRange',
                            'operator', 'between',
                            'value', jsonb_build_object('min', (g % 60), 'max', 100)),
                        jsonb_build_object(
                            'field', 'traffic',
                            'kind', 'numberRange',
                            'operator', 'gte',
                            'value', jsonb_build_object('min', ((g % 20) + 1) * 1000)),
                        jsonb_build_object(
                            'field', 'priceUsd',
                            'kind', 'numberRange',
                            'operator', 'between',
                            'value', jsonb_build_object('min', (g % 10) * 50, 'max', ((g % 10) * 50) + 500))
                    ))
            END AS filters_snapshot
        FROM generate_series(@startValue, @endValue) AS series(g)
    )
    INSERT INTO "ExportLogs" (
        "Id",
        "UserId",
        "UserEmail",
        "Role",
        "TimestampUtc",
        "RowsReturned",
        "RequestedRowsCount",
        "ExportedRowsCount",
        "WasTruncated",
        "ExportLimitRows",
        "DailyUniqueExportedDomainsLimit",
        "WeeklyUniqueExportedDomainsLimit",
        "DailyExportOperationsLimit",
        "WeeklyExportOperationsLimit",
        "Destination",
        "ExportMode",
        "BlockedReason"
    )
    SELECT
        log_id,
        user_id,
        user_email,
        'Client',
        timestamp_utc,
        exported_rows,
        requested_rows,
        exported_rows,
        was_truncated,
        5000,
        1000,
        3000,
        20,
        60,
        destination,
        'Sites',
        blocked_reason
    FROM generated
    ON CONFLICT ("Id") DO NOTHING;

    WITH generated AS (
        SELECT
            g,
            (
                substr(md5(@runId || ':log:' || g::text), 1, 8) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 9, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 13, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 17, 4) || '-' ||
                substr(md5(@runId || ':log:' || g::text), 21, 12)
            )::uuid AS log_id,
            (
                substr(md5(@runId || ':snapshot:' || g::text), 1, 8) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 9, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 13, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 17, 4) || '-' ||
                substr(md5(@runId || ':snapshot:' || g::text), 21, 12)
            )::uuid AS snapshot_id,
            @fromUtc::timestamptz + ((g % @rangeSeconds) * interval '1 second') AS timestamp_utc,
            CASE
                WHEN g % 25 = 0 THEN jsonb_build_object('schemaVersion', 1, 'filters', '[]'::jsonb)
                ELSE jsonb_build_object(
                    'schemaVersion', 1,
                    'filters', jsonb_build_array(
                        jsonb_build_object('field', 'locationKey', 'kind', 'multiSelect', 'operator', 'anyOf', 'value', to_jsonb(ARRAY[CASE g % 6 WHEN 0 THEN 'US' WHEN 1 THEN 'GB' WHEN 2 THEN 'DE' WHEN 3 THEN 'FR' WHEN 4 THEN 'CA' ELSE 'AU' END]::text[])),
                        jsonb_build_object('field', 'niche', 'kind', 'multiSelect', 'operator', 'anyOf', 'value', to_jsonb(ARRAY[CASE g % 5 WHEN 0 THEN 'casino' WHEN 1 THEN 'crypto' WHEN 2 THEN 'finance' WHEN 3 THEN 'travel' ELSE 'technology' END]::text[])),
                        jsonb_build_object('field', 'categories', 'kind', 'textSearch', 'operator', 'containsAny', 'value', to_jsonb(ARRAY[CASE g % 5 WHEN 0 THEN 'Finance' WHEN 1 THEN 'Business' WHEN 2 THEN 'News' WHEN 3 THEN 'Lifestyle' ELSE 'Technology' END]::text[])),
                        jsonb_build_object('field', 'language', 'kind', 'multiSelect', 'operator', 'anyOf', 'value', to_jsonb(ARRAY[CASE g % 4 WHEN 0 THEN 'en' WHEN 1 THEN 'de' WHEN 2 THEN 'fr' ELSE 'es' END]::text[])),
                        jsonb_build_object('field', 'priceCasinoAvailability', 'kind', 'availability', 'operator', 'in', 'value', CASE WHEN g % 11 = 0 THEN to_jsonb(ARRAY['available', 'notAvailable']::text[]) WHEN g % 3 = 0 THEN to_jsonb(ARRAY['notAvailable']::text[]) ELSE to_jsonb(ARRAY['available']::text[]) END),
                        jsonb_build_object('field', 'dr', 'kind', 'numberRange', 'operator', 'between', 'value', jsonb_build_object('min', (g % 60), 'max', 100)),
                        jsonb_build_object('field', 'traffic', 'kind', 'numberRange', 'operator', 'gte', 'value', jsonb_build_object('min', ((g % 20) + 1) * 1000)),
                        jsonb_build_object('field', 'priceUsd', 'kind', 'numberRange', 'operator', 'between', 'value', jsonb_build_object('min', (g % 10) * 50, 'max', ((g % 10) * 50) + 500))
                    ))
            END AS filters_snapshot
        FROM generate_series(@startValue, @endValue) AS series(g)
    )
    INSERT INTO "ExportAnalyticsSnapshots" (
        "Id",
        "ExportLogId",
        "SnapshotVersion",
        "FiltersSnapshotJson",
        "SortSnapshotJson",
        "SearchSnapshotJson",
        "CreatedAtUtc"
    )
    SELECT
        snapshot_id,
        log_id,
        1,
        filters_snapshot,
        '{"schemaVersion":1,"sorts":[]}'::jsonb,
        NULL,
        timestamp_utc
    FROM generated
    WHERE EXISTS (
        SELECT 1
        FROM "ExportLogs" log
        WHERE log."Id" = generated.log_id
    )
    ON CONFLICT ("ExportLogId") DO NOTHING;
    """;
}

file sealed record LoginRequest(string Email, string Password, bool RememberMe);

file sealed record EndpointMeasurement(HttpStatusCode StatusCode, TimeSpan Elapsed, int ResponseBytes);

file sealed record BenchmarkOptions
{
    public string ConnectionString { get; private init; } =
        "Host=localhost;Port=5433;Database=redhead_sites_catalog;Username=postgres;Password=postgres";
    public Uri BaseUrl { get; private init; } = new("http://localhost:5000");
    public string? Email { get; private init; }
    public string? Password { get; private init; }
    public long Rows { get; private init; } = 5_000_000;
    public int BatchSize { get; private init; } = 100_000;
    public int Runs { get; private init; } = 3;
    public int Warmups { get; private init; } = 1;
    public int TargetMilliseconds { get; private init; } = 10_000;
    public int CommandTimeoutSeconds { get; private init; } = 0;
    public string RunId { get; private init; } = "business-demand-analytics-load-test-v1";
    public DateOnly From { get; private init; } = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-364);
    public DateOnly To { get; private init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public bool Seed { get; private init; }
    public bool Cleanup { get; private init; }
    public bool Measure { get; private init; }
    public bool ShowHelp { get; private init; }
    public bool HasAction => Seed || Cleanup || Measure;

    public static BenchmarkOptions Parse(string[] args)
    {
        var options = new BenchmarkOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var value = i + 1 < args.Length ? args[i + 1] : null;

            switch (arg)
            {
                case "--help" or "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--seed":
                    options = options with { Seed = true };
                    break;
                case "--cleanup":
                    options = options with { Cleanup = true };
                    break;
                case "--measure":
                    options = options with { Measure = true };
                    break;
                case "--connection-string":
                    options = options with { ConnectionString = RequiredValue(arg, value) };
                    i++;
                    break;
                case "--base-url":
                    options = options with { BaseUrl = new Uri(RequiredValue(arg, value).TrimEnd('/')) };
                    i++;
                    break;
                case "--email":
                    options = options with { Email = RequiredValue(arg, value) };
                    i++;
                    break;
                case "--password":
                    options = options with { Password = RequiredValue(arg, value) };
                    i++;
                    break;
                case "--rows":
                    options = options with { Rows = ParseLong(arg, value, min: 1) };
                    i++;
                    break;
                case "--batch-size":
                    options = options with { BatchSize = ParseInt(arg, value, min: 1) };
                    i++;
                    break;
                case "--runs":
                    options = options with { Runs = ParseInt(arg, value, min: 1) };
                    i++;
                    break;
                case "--warmups":
                    options = options with { Warmups = ParseInt(arg, value, min: 0) };
                    i++;
                    break;
                case "--target-ms":
                    options = options with { TargetMilliseconds = ParseInt(arg, value, min: 1) };
                    i++;
                    break;
                case "--command-timeout-seconds":
                    options = options with { CommandTimeoutSeconds = ParseInt(arg, value, min: 0) };
                    i++;
                    break;
                case "--run-id":
                    options = options with { RunId = RequiredValue(arg, value) };
                    i++;
                    break;
                case "--from":
                    options = options with { From = ParseDate(arg, value) };
                    i++;
                    break;
                case "--to":
                    options = options with { To = ParseDate(arg, value) };
                    i++;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (options.From > options.To)
        {
            throw new ArgumentException("--from must be earlier than or equal to --to.");
        }

        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine(
            """
            Business Demand Analytics local load benchmark

            Required action flags:
              --seed                  Seed deterministic synthetic export analytics rows.
              --measure               Login and measure GET /api/admin/analytics/business-demand.
              --cleanup               Delete benchmark rows. Deletes rows with UserId analytics-benchmark-client-%.

            Common options:
              --rows 5000000          Target synthetic ExportLogs rows. Default: 5,000,000.
              --from yyyy-MM-dd       Benchmark query start date. Default: UTC today - 364 days.
              --to yyyy-MM-dd         Benchmark query end date. Default: UTC today.
              --target-ms 10000       Fails with exit code 2 when max measured response time exceeds this.
              --base-url URL          API base URL. Default: http://localhost:5000.
              --email EMAIL           SuperAdmin email for measurement.
              --password PASSWORD     SuperAdmin password for measurement.
              --connection-string CS  PostgreSQL connection string. Default: local docker-compose.dev.yml DB.

            Less common options:
              --batch-size 100000
              --runs 3
              --warmups 1
              --command-timeout-seconds 0
              --run-id business-demand-analytics-load-test-v1

            Examples:
              dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- --seed --rows 5000000
              dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- --measure --email super@example.com --password "password"
              dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- --cleanup
            """);
    }

    private static string RequiredValue(string arg, string? value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{arg} requires a value.")
            : value;

    private static int ParseInt(string arg, string? value, int min)
    {
        if (!int.TryParse(RequiredValue(arg, value), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min)
        {
            throw new ArgumentException($"{arg} must be an integer >= {min}.");
        }

        return parsed;
    }

    private static long ParseLong(string arg, string? value, long min)
    {
        if (!long.TryParse(RequiredValue(arg, value), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min)
        {
            throw new ArgumentException($"{arg} must be an integer >= {min}.");
        }

        return parsed;
    }

    private static DateOnly ParseDate(string arg, string? value)
    {
        if (!DateOnly.TryParseExact(
                RequiredValue(arg, value),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new ArgumentException($"{arg} must be yyyy-MM-dd.");
        }

        return parsed;
    }
}
