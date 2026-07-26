using System.Reflection;
using HotelPOS.Common;
using HotelPOS.Logging;

namespace HotelPOS.Data;

/// <summary>
/// รัน schema.sql (ฝังอยู่ใน dll เป็น EmbeddedResource) เพื่อสร้าง/อัปเดตตารางฐานข้อมูล
/// ออกแบบให้รันซ้ำได้ปลอดภัย (schema.sql เขียนด้วย IF NOT EXISTS ทั้งหมด)
/// ในอนาคตถ้ามีการแก้ schema เพิ่ม ให้เพิ่มไฟล์ schema_v2.sql, schema_v3.sql ... แล้ว apply ตามลำดับ
/// โดยเช็คจากตาราง schema_migrations ว่าอยู่เวอร์ชันไหนแล้ว
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

    private static string ReadEmbeddedSchemaSql()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // ชื่อ resource = <RootNamespace>.<โฟลเดอร์>.<ไฟล์> ตามที่ตั้งใน .csproj (EmbeddedResource)
        const string resourceName = "HotelPOS.Data.Database.schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"ไม่พบไฟล์ schema.sql ที่ฝังไว้ใน assembly (resource name: {resourceName})");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
