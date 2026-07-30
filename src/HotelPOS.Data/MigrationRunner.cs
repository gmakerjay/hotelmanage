using System.Reflection;
using HotelPOS.Common;
using HotelPOS.Logging;

namespace HotelPOS.Data;

/// <summary>
/// รัน schema.sql (ฝังอยู่ใน dll เป็น EmbeddedResource) เพื่อสร้าง/อัปเดตตารางฐานข้อมูล
/// ออกแบบให้รันซ้ำได้ปลอดภัย (schema.sql เขียนด้วย IF NOT EXISTS ทั้งหมด)
/// มีระบบ Auto-Migration เพิ่มคอลัมน์อัตโนมัติหากฐานข้อมูลเดิมยังมีคอลัมน์ไม่ครบ
/// </summary>
public class MigrationRunner
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public MigrationRunner(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public void EnsureDatabaseIsReady()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            _logger.Info(LogCategory.Database, "เริ่มตรวจสอบ/สร้างฐานข้อมูล", correlationId);

            var sql = ReadEmbeddedSchemaSql();

            using var connection = _connectionFactory.CreateConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();

            // Auto-Migrate missing columns for existing SQLite databases
            EnsureUtilityBillColumnsExist(connection);
            EnsureCustomerColumnsExist(connection);
            EnsureRoomTypeBillingColumnsExist(connection);

            transaction.Commit();

            _logger.Info(LogCategory.Database,
                $"ฐานข้อมูลพร้อมใช้งานที่ {_connectionFactory.DatabaseFilePath}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "ไม่สามารถสร้าง/อัปเดตฐานข้อมูลได้", ex, correlationId);
            throw;
        }
    }

    private static void EnsureUtilityBillColumnsExist(System.Data.IDbConnection connection)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(utility_bills);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1)); // Column name is index 1
            }
        }

        var columnsToAdd = new (string colName, string colDef)[]
        {
            ("electric_prev", "NUMERIC NOT NULL DEFAULT 0"),
            ("electric_curr", "NUMERIC NOT NULL DEFAULT 0"),
            ("electric_units", "NUMERIC NOT NULL DEFAULT 0"),
            ("electric_rate", "NUMERIC NOT NULL DEFAULT 0"),
            ("water_prev", "NUMERIC NOT NULL DEFAULT 0"),
            ("water_curr", "NUMERIC NOT NULL DEFAULT 0"),
            ("water_units", "NUMERIC NOT NULL DEFAULT 0"),
            ("water_rate", "NUMERIC NOT NULL DEFAULT 0"),
            ("electric_billing_mode", "TEXT NOT NULL DEFAULT 'METER'")
        };

        foreach (var (colName, colDef) in columnsToAdd)
        {
            if (!existingColumns.Contains(colName))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE utility_bills ADD COLUMN {colName} {colDef};";
                cmd.ExecuteNonQuery();
            }
        }
    }

    private static void EnsureCustomerColumnsExist(System.Data.IDbConnection connection)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(customers);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (!existingColumns.Contains("id_card_or_passport"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE customers ADD COLUMN id_card_or_passport TEXT;";
            cmd.ExecuteNonQuery();

            // หากมีคอลัมน์ id_card_number เดิม ให้ย้ายข้อมูลมาใส่ id_card_or_passport
            if (existingColumns.Contains("id_card_number"))
            {
                using var copyCmd = connection.CreateCommand();
                copyCmd.CommandText = "UPDATE customers SET id_card_or_passport = id_card_number WHERE id_card_or_passport IS NULL;";
                copyCmd.ExecuteNonQuery();
            }
        }
    }

    private static void EnsureRoomTypeBillingColumnsExist(System.Data.IDbConnection connection)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(room_types);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var columnsToAdd = new (string colName, string colDef)[]
        {
            ("electric_billing_mode", "INTEGER NOT NULL DEFAULT 0"),
            ("electric_flat_rate", "NUMERIC NOT NULL DEFAULT 0"),
            ("water_billing_mode", "INTEGER NOT NULL DEFAULT 0"),
            ("water_flat_rate", "NUMERIC NOT NULL DEFAULT 0"),
            ("color_hex", "TEXT DEFAULT '#3B82F6'")
        };

        foreach (var (colName, colDef) in columnsToAdd)
        {
            if (!existingColumns.Contains(colName))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE room_types ADD COLUMN {colName} {colDef};";
                cmd.ExecuteNonQuery();
            }
        }
    }

    private static string ReadEmbeddedSchemaSql()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "HotelPOS.Data.Database.schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"ไม่พบไฟล์ schema.sql ที่ฝังไว้ใน assembly (resource name: {resourceName})");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
