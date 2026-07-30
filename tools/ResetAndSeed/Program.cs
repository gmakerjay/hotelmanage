// HotelPOS - Reset & Seed Database Script
// ลบ DB เดิม, สร้างใหม่, เพิ่มข้อมูลตัวอย่าง

using Microsoft.Data.Sqlite;

var dbDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "PSoftRestRentManager");
var dbPath = Path.Combine(dbDir, "restrent.db");
var walPath = dbPath + "-wal";
var shmPath = dbPath + "-shm";

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Step 1: ลบ DB เดิม
Console.WriteLine("=== HotelPOS - Reset & Seed Database ===");
Console.WriteLine();

SqliteConnection.ClearAllPools();

if (File.Exists(dbPath)) { File.Delete(dbPath); Console.WriteLine($"[OK] ลบไฟล์ DB: {dbPath}"); }
if (File.Exists(walPath)) { File.Delete(walPath); Console.WriteLine($"[OK] ลบไฟล์ WAL"); }
if (File.Exists(shmPath)) { File.Delete(shmPath); Console.WriteLine($"[OK] ลบไฟล์ SHM"); }

var assetsDir = Path.Combine(dbDir, "assets");
if (Directory.Exists(assetsDir)) { Directory.Delete(assetsDir, true); Console.WriteLine("[OK] ลบโฟลเดอร์ assets"); }

if (!Directory.Exists(dbDir))
    Directory.CreateDirectory(dbDir);

Console.WriteLine();

// Step 2: สร้าง DB ใหม่จาก schema.sql
var schemaPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HotelPOS.Data", "Database", "schema.sql");
// Try alternate path if running from tools
if (!File.Exists(schemaPath))
{
    schemaPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "HotelPOS.Data", "Database", "schema.sql");
}
if (!File.Exists(schemaPath))
{
    // Try direct path
    schemaPath = @"c:\Users\admin\Documents\HotelPOS\src\HotelPOS.Data\Database\schema.sql";
}

Console.WriteLine($"[INFO] Schema path: {schemaPath}");
var schemaSql = File.ReadAllText(schemaPath);

var connStr = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared,
    ForeignKeys = true
}.ConnectionString;

using (var conn = new SqliteConnection(connStr))
{
    conn.Open();
    
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
    }

    // Run schema
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = schemaSql;
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("[OK] สร้างตาราง DB จาก schema.sql สำเร็จ");

    // Step 3: Seed data
    var seedPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "seed_data.sql");
    if (!File.Exists(seedPath))
    {
        seedPath = @"c:\Users\admin\Documents\HotelPOS\tools\seed_data.sql";
    }

    Console.WriteLine($"[INFO] Seed path: {seedPath}");
    var seedSql = File.ReadAllText(seedPath);

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = seedSql;
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("[OK] Seed ข้อมูลตัวอย่างสำเร็จ");

    // Verify
    Console.WriteLine();
    Console.WriteLine("=== ตรวจสอบข้อมูล ===");
    
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM room_types";
        Console.WriteLine($"  ประเภทห้อง: {cmd.ExecuteScalar()} รายการ");
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM rooms";
        Console.WriteLine($"  ห้องพัก: {cmd.ExecuteScalar()} ห้อง");
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM customers";
        Console.WriteLine($"  ลูกค้า: {cmd.ExecuteScalar()} คน");
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM users";
        Console.WriteLine($"  ผู้ใช้: {cmd.ExecuteScalar()} คน");
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM roles";
        Console.WriteLine($"  Role: {cmd.ExecuteScalar()} รายการ");
    }

    Console.WriteLine();
    Console.WriteLine("=== รายละเอียดห้อง ===");
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT r.room_number, r.floor, rt.name, rt.daily_rate, rt.monthly_rate 
            FROM rooms r 
            JOIN room_types rt ON r.room_type_id = rt.id 
            ORDER BY r.floor, r.room_number";
        using var reader = cmd.ExecuteReader();
        Console.WriteLine($"  {"ห้อง",-8} {"ชั้น",-6} {"ประเภท",-20} {"รายวัน",-10} {"รายเดือน",-10}");
        Console.WriteLine($"  {new string('-', 54)}");
        while (reader.Read())
        {
            Console.WriteLine($"  {reader.GetString(0),-8} {reader.GetString(1),-6} {reader.GetString(2),-20} {reader.GetDecimal(3),-10:N0} {reader.GetDecimal(4),-10:N0}");
        }
    }
}

Console.WriteLine();
Console.WriteLine("[DONE] ระบบพร้อมใช้งาน! เปิดแอพ HotelPOS ใหม่ได้เลยครับ");
