using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly ISettingsService _settingsService;

    public SettingsServiceTests()
    {
        // ใช้ไฟล์ DB/Log ชั่วคราวแยกทุกครั้งที่ทดสอบ เพื่อไม่ให้เทสกระทบกัน
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-test-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        ISettingsRepository repository = new SettingsRepository(_connectionFactory, _logger);
        _settingsService = new SettingsService(repository, _logger);
    }

    [Fact]
    public async Task GetShopNameAsync_ควรได้ค่าเริ่มต้นจาก_seed_data()
    {
        var shopName = await _settingsService.GetShopNameAsync();
        Assert.False(string.IsNullOrWhiteSpace(shopName));
    }

    [Fact]
    public async Task SetAsync_แล้ว_GetAsync_ควรได้ค่าที่เพิ่งบันทึก()
    {
        await _settingsService.SetAsync("shop_name", "โรงแรมทดสอบ");
        var result = await _settingsService.GetAsync("shop_name");
        Assert.Equal("โรงแรมทดสอบ", result);
    }

    [Fact]
    public async Task GetNextDocumentNumberAsync_ควรออกเลขที่รันต่อเนื่องไม่ซ้ำกัน()
    {
        var first = await _settingsService.GetNextDocumentNumberAsync();
        var second = await _settingsService.GetNextDocumentNumberAsync();
        Assert.NotEqual(first, second);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }

        // เคลียร์ Connection Pool ของ SQLite เพื่อปลดล็อกไฟล์ฐานข้อมูล
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // รอและพยายามลบไฟล์ชั่วคราว (เผื่อกรณีระบบปฏิบัติการประมวลผลการปลดล็อกไฟล์ช้า)
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_tempDbPath))
                {
                    File.Delete(_tempDbPath);
                }
                if (Directory.Exists(_tempLogPath))
                {
                    Directory.Delete(_tempLogPath, recursive: true);
                }
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
