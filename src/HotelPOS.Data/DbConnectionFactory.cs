using Microsoft.Data.Sqlite;

namespace HotelPOS.Data;

/// <summary>
/// จุดเดียวที่รู้ว่าไฟล์ฐานข้อมูลอยู่ที่ไหน และสร้าง connection ยังไง
/// ห้ามให้โปรเจคอื่น (โดยเฉพาะ HotelPOS.UI) เปิด connection เองตรงๆ
/// </summary>
public class DbConnectionFactory
{
    private readonly string _dbFilePath;

    public DbConnectionFactory(string? dbFilePath = null)
    {
        // ค่าเริ่มต้น: เก็บไฟล์ DB ไว้ใน %AppData%\HotelPOS\hotelpos.db
        // เลือกใช้ AppData แทน Program Files เพราะ Program Files มักไม่มีสิทธิ์เขียนไฟล์บนเครื่องลูกค้า
        _dbFilePath = dbFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotelPOS",
            "hotelpos.db");

        var dir = Path.GetDirectoryName(_dbFilePath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string DatabaseFilePath => _dbFilePath;

    public bool DatabaseFileExists => File.Exists(_dbFilePath);

    /// <summary>สร้าง connection ใหม่ (เรียกใช้ใน using statement เสมอ)</summary>
    public SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        // เปิด WAL mode เพื่อ performance ที่ดีขึ้นและลดปัญหา "database is locked"
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        return connection;
    }
}
