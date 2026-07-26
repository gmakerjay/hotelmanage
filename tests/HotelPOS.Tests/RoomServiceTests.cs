using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class RoomServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomService _roomService;

    public RoomServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-roomtest-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-roomtest-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        _roomRepository = new RoomRepository(_connectionFactory, _logger);
        _roomService = new RoomService(_roomRepository, _logger);
    }

    [Fact]
    public async Task SaveRoomTypeAsync_ควรสร้างและดึงประเภทห้องพักสำเร็จ()
    {
        var roomType = new RoomType
        {
            Name = "Deluxe Sea View",
            DailyRate = 1200,
            HourlyRate = 300,
            MonthlyRate = 15000,
            Description = "ห้องสวยวิวทะเล"
        };

        var id = await _roomService.SaveRoomTypeAsync(roomType);
        Assert.True(id > 0);

        var fetched = await _roomService.GetRoomTypeByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Deluxe Sea View", fetched!.Name);
        Assert.Equal(1200, fetched.DailyRate);
    }

    [Fact]
    public async Task SaveRoomAsync_ควรสร้างห้องพักและป้องกันเลขห้องซ้ำ()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 600 });

        var room = new Room
        {
            RoomNumber = "101",
            Floor = "1",
            RoomTypeId = typeId,
            Status = RoomStatus.Available
        };

        var roomId = await _roomService.SaveRoomAsync(room);
        Assert.True(roomId > 0);

        // ทดสอบสร้างห้องเลขซ้ำ 101 ต้อง throw InvalidOperationException
        var duplicateRoom = new Room
        {
            RoomNumber = "101",
            Floor = "1",
            RoomTypeId = typeId
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _roomService.SaveRoomAsync(duplicateRoom));
    }

    [Fact]
    public async Task UpdateRoomStatusAsync_ควรเปลี่ยนสถานะห้องพักได้ถูกต้อง()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 600 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "201", Floor = "2", RoomTypeId = typeId });

        await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Maintenance, "ปิดซ่อมแอร์");

        var fetched = await _roomService.GetRoomByIdAsync(roomId);
        Assert.NotNull(fetched);
        Assert.Equal(RoomStatus.Maintenance, fetched!.Status);
        Assert.Equal("ปิดซ่อมแอร์", fetched.Notes);
    }

    [Fact]
    public async Task DeleteRoomAsync_ควรปฏิเสธการลบถ้าห้องมีคนเข้าพักอยู่()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 600 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "301", Floor = "3", RoomTypeId = typeId });

        await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Occupied);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _roomService.DeleteRoomAsync(roomId));
        Assert.Contains("มีผู้เข้าพัก", ex.Message);
    }

    [Fact]
    public async Task DeleteRoomTypeAsync_ควรปฏิเสธการลบประเภทห้องถ้ายังมีห้องผูกอยู่()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Vip Suite", DailyRate = 2500 });
        await _roomService.SaveRoomAsync(new Room { RoomNumber = "401", Floor = "4", RoomTypeId = typeId });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _roomService.DeleteRoomTypeAsync(typeId));
        Assert.Contains("มีห้องพักจำนวน 1 ห้องที่ใช้งานประเภทนี้อยู่", ex.Message);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
                if (Directory.Exists(_tempLogPath)) Directory.Delete(_tempLogPath, recursive: true);
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
