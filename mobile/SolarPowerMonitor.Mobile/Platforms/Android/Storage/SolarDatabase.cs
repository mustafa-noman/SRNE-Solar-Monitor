using Android.Content;
using Android.Database.Sqlite;
using SolarPowerMonitor.Mobile.Models;

namespace SolarPowerMonitor.Mobile.Services;

public static class SolarDatabase
{
    private const string DatabaseFileName = "solar-monitor.db3";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static SolarDatabaseHelper? helper;

    private static SolarDatabaseHelper Helper =>
        helper ??= new SolarDatabaseHelper(Android.App.Application.Context);

    public static async Task InitializeAsync()
    {
        await Gate.WaitAsync();
        try
        {
            _ = Helper.WritableDatabase;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<string?> GetSettingAsync(string key)
    {
        await Gate.WaitAsync();
        try
        {
            using var cursor = Helper.ReadableDatabase!.Query(
                "settings", ["value"], "key = ?", [key], null, null, null, "1");
            return cursor.MoveToFirst() ? cursor.GetString(0) : null;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task SaveSettingsAsync(IReadOnlyDictionary<string, string> settings)
    {
        await Gate.WaitAsync();
        try
        {
            var database = Helper.WritableDatabase!;
            database.BeginTransaction();
            try
            {
                foreach (var setting in settings)
                {
                    using var values = new ContentValues();
                    values.Put("key", setting.Key);
                    values.Put("value", setting.Value);
                    values.Put("updated_utc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    database.InsertWithOnConflict("settings", null, values, Conflict.Replace);
                }
                database.SetTransactionSuccessful();
            }
            finally
            {
                database.EndTransaction();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task InsertTelemetryAsync(
        SolarTelemetry telemetry,
        string source,
        string collectionKind,
        DateTimeOffset recordedAt)
    {
        await Gate.WaitAsync();
        try
        {
            using var values = new ContentValues();
            values.Put("recorded_utc", recordedAt.ToUnixTimeMilliseconds());
            values.Put("source", source);
            values.Put("collection_kind", collectionKind);
            values.Put("solar_watts", (int)telemetry.PvChargingPower);
            values.Put("battery_percent", (int)telemetry.BatteryStateOfCharge);
            values.Put("pv_voltage", (double)telemetry.PvArrayVoltage);
            values.Put("pv_current", (double)telemetry.PvArrayCurrent);
            values.Put("battery_voltage", (double)telemetry.BatteryVoltage);
            values.Put("charge_current", (double)telemetry.BatteryChargingCurrent);
            Helper.WritableDatabase!.InsertOrThrow("telemetry", null, values);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<IReadOnlyList<HistoryPoint>> GetHistoryAsync(int limit = 500)
    {
        await Gate.WaitAsync();
        try
        {
            using var cursor = Helper.ReadableDatabase!.Query(
                "telemetry",
                ["recorded_utc", "solar_watts", "battery_percent"],
                null,
                null,
                null,
                null,
                "recorded_utc DESC",
                Math.Clamp(limit, 1, 10_000).ToString());
            var results = new List<HistoryPoint>(cursor.Count);
            while (cursor.MoveToNext())
            {
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(0)).LocalDateTime;
                results.Add(new HistoryPoint(timestamp, checked((ushort)cursor.GetInt(1)), checked((ushort)cursor.GetInt(2))));
            }
            return results;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<StorageStats> GetStatsAsync()
    {
        await Gate.WaitAsync();
        try
        {
            using var cursor = Helper.ReadableDatabase!.RawQuery(
                "SELECT COUNT(*), MAX(CASE WHEN collection_kind = 'background' THEN recorded_utc END) FROM telemetry",
                null);
            if (!cursor.MoveToFirst())
                return new StorageStats(0, null);

            var lastBackground = cursor.IsNull(1)
                ? (DateTimeOffset?)null
                : DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(1));
            return new StorageStats(cursor.GetLong(0), lastBackground);
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed class SolarDatabaseHelper(Context context) : SQLiteOpenHelper(context, DatabaseFileName, null, 1)
    {
        public override void OnCreate(SQLiteDatabase? database)
        {
            ArgumentNullException.ThrowIfNull(database);
            database.ExecSQL(
                """
                CREATE TABLE settings (
                    key TEXT PRIMARY KEY NOT NULL,
                    value TEXT NOT NULL,
                    updated_utc INTEGER NOT NULL
                )
                """);
            database.ExecSQL(
                """
                CREATE TABLE telemetry (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    recorded_utc INTEGER NOT NULL,
                    source TEXT NOT NULL,
                    collection_kind TEXT NOT NULL,
                    solar_watts INTEGER NOT NULL,
                    battery_percent INTEGER NOT NULL,
                    pv_voltage REAL NOT NULL,
                    pv_current REAL NOT NULL,
                    battery_voltage REAL NOT NULL,
                    charge_current REAL NOT NULL
                )
                """);
            database.ExecSQL("CREATE INDEX ix_telemetry_recorded_utc ON telemetry(recorded_utc DESC)");
        }

        public override void OnConfigure(SQLiteDatabase? database)
        {
            ArgumentNullException.ThrowIfNull(database);
            base.OnConfigure(database);
            database.SetForeignKeyConstraintsEnabled(true);
            database.EnableWriteAheadLogging();
        }

        public override void OnUpgrade(SQLiteDatabase? database, int oldVersion, int newVersion)
        {
            ArgumentNullException.ThrowIfNull(database);
        }
    }
}

public sealed record StorageStats(long ReadingCount, DateTimeOffset? LastBackgroundUtc);
