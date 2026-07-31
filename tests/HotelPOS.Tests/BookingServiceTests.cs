using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Core.Services;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class BookingServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempLogPath;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IFolioRepository _folioRepository;

    private readonly IRoomService _roomService;
    private readonly ICustomerService _customerService;
    private readonly IBookingService _bookingService;

    public BookingServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bookingtest-{Guid.NewGuid():N}.db");
        _tempLogPath = Path.Combine(Path.GetTempPath(), $"hotelpos-bookingtest-logs-{Guid.NewGuid():N}");

        _connectionFactory = new DbConnectionFactory(_tempDbPath);
        _logger = new AppLogger(_tempLogPath);

        new MigrationRunner(_connectionFactory, _logger).EnsureDatabaseIsReady();

        _roomRepository = new RoomRepository(_connectionFactory, _logger);
        _bookingRepository = new BookingRepository(_connectionFactory, _logger);
        _customerRepository = new CustomerRepository(_connectionFactory, _logger);
        _folioRepository = new FolioRepository(_connectionFactory, _logger);

        _roomService = new RoomService(_roomRepository, _logger);
        _customerService = new CustomerService(_customerRepository, _logger);
        _bookingService = new BookingService(_bookingRepository, _roomRepository, _customerRepository, _folioRepository, _logger);
    }

    [Fact]
    public async Task WalkInCheckInAsync_ควรเปลี่ยนสถานะห้องเป็น_Occupied_และสร้าง_Folio()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Deluxe", DailyRate = 1000 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "101", Floor = "1", RoomTypeId = typeId });

        var customer = new Customer { FullName = "สมชาย ใจดี", Phone = "0812345678" };
        var checkOutPlanned = DateTime.Now.AddDays(2);

        var booking = await _bookingService.WalkInCheckInAsync(
            roomId,
            customer,
            RatePlanType.Daily,
            1000,
            checkOutPlanned,
            "Walk-in ทั่วไป"
        );

        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);

        // ตรวจสอบสถานะห้องพัก
        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Occupied, room!.Status);

        // ตรวจสอบ Folio
        var folio = await _bookingService.GetFolioByBookingIdAsync(booking.Id);
        Assert.NotNull(folio);
        Assert.False(folio!.IsClosed);
        Assert.Equal(2000, folio.TotalAmount); // 2 คืน * 1000 บาท
    }

    [Fact]
    public async Task AdvanceReservation_และ_CheckInExistingBooking_ทำงานถูกต้อง()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Standard", DailyRate = 500 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "102", Floor = "1", RoomTypeId = typeId });

        var customer = new Customer { FullName = "สมหญิง มีสุข", Phone = "0898765432" };
        var checkInPlanned = DateTime.Now.AddDays(1);
        var checkOutPlanned = DateTime.Now.AddDays(3);

        var booking = await _bookingService.CreateReservationAsync(
            roomId,
            customer,
            RatePlanType.Daily,
            500,
            checkInPlanned,
            checkOutPlanned
        );

        Assert.Equal(BookingStatus.Reserved, booking.Status);

        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Reserved, room!.Status);

        // เช็คอินจากการจอง
        await _bookingService.CheckInExistingBookingAsync(booking.Id);

        var updatedBooking = await _bookingService.GetBookingByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.CheckedIn, updatedBooking!.Status);
        Assert.NotNull(updatedBooking.CheckInActual);

        var updatedRoom = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Occupied, updatedRoom!.Status);
    }

    [Fact]
    public async Task CheckOutAsync_ควรสรุปบิลปิด_Folio_และเปลี่ยนสถานะห้องเป็น_Cleaning()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Suite", DailyRate = 2000 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "301", Floor = "3", RoomTypeId = typeId });

        var customer = new Customer { FullName = "นายวิชัย สุขใจ", Phone = "0822223333" };

        var booking = await _bookingService.WalkInCheckInAsync(
            roomId,
            customer,
            RatePlanType.Daily,
            2000,
            DateTime.Now.AddDays(1)
        );

        // ทำการเช็คเอาท์พร้อมเพิ่มค่าบริการเสริม 300 และส่วนลด 100
        var folio = await _bookingService.CheckOutAsync(booking.Id, extraCharges: 300, discountAmount: 100);

        Assert.True(folio.IsClosed);
        Assert.Equal(2000, folio.RoomCharges);
        Assert.Equal(300, folio.ExtraCharges);
        Assert.Equal(100, folio.DiscountAmount);
        Assert.Equal(2200, folio.TotalAmount); // 2000 + 300 - 100 = 2200

        var updatedBooking = await _bookingService.GetBookingByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.CheckedOut, updatedBooking!.Status);
        Assert.NotNull(updatedBooking.CheckOutActual);

        var room = await _roomService.GetRoomByIdAsync(roomId);
        Assert.Equal(RoomStatus.Cleaning, room!.Status);
    }

    [Fact]
    public async Task WalkInMonthly_เช็คเอาท์ทันที_ควรคิดค่าเช่าอย่างน้อย_1_เดือน()
    {
        var typeId = await _roomService.SaveRoomTypeAsync(new RoomType { Name = "Studio", DailyRate = 5000 });
        var roomId = await _roomService.SaveRoomAsync(new Room { RoomNumber = "401", Floor = "4", RoomTypeId = typeId });

        var customer = new Customer { FullName = "นายรายเดือน ทดสอบ", Phone = "0811111111" };

        var booking = await _bookingService.WalkInCheckInAsync(
            roomId,
            customer,
            RatePlanType.Monthly,
            5000,
            DateTime.Now.AddMonths(1)
        );

        // เช็คเอาท์ทันที → ต้องคิดอย่างน้อย 1 เดือน = 5,000 บาท
        var folio = await _bookingService.CheckOutAsync(booking.Id);
        Assert.True(folio.RoomCharges >= 5000, $"ค่าเช่ารายเดือนต้อง >= 5000 (ได้ {folio.RoomCharges})");
    }

    [Fact]
    public void CalculateRoomCharges_Monthly_อยู่75วัน_ต้องคิด3เดือน()
    {
        // ทดสอบ private static method ผ่าน Reflection เพื่อตรวจสอบการคำนวณจำนวนเดือน
        var method = typeof(BookingService).GetMethod("CalculateRoomCharges",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var start = new DateTime(2026, 1, 1);

        // Case 1: อยู่ 75 วัน → Ceiling(75/30) = 3 เดือน → 3 * 3500 = 10500
        var result75 = (decimal)method!.Invoke(null, new object[] { RatePlanType.Monthly, 3500m, start, start.AddDays(75) })!;
        Assert.Equal(10500m, result75);

        // Case 2: อยู่ 30 วันพอดี → Ceiling(30/30) = 1 เดือน → 1 * 3500 = 3500
        var result30 = (decimal)method.Invoke(null, new object[] { RatePlanType.Monthly, 3500m, start, start.AddDays(30) })!;
        Assert.Equal(3500m, result30);

        // Case 3: อยู่ 31 วัน → Ceiling(31/30) = 2 เดือน → 2 * 3500 = 7000
        var result31 = (decimal)method.Invoke(null, new object[] { RatePlanType.Monthly, 3500m, start, start.AddDays(31) })!;
        Assert.Equal(7000m, result31);

        // Case 4: อยู่ 1 วัน → Ceiling(1/30) = 1 เดือน → 1 * 3500 = 3500 (ขั้นต่ำ 1 เดือน)
        var result1 = (decimal)method.Invoke(null, new object[] { RatePlanType.Monthly, 3500m, start, start.AddDays(1) })!;
        Assert.Equal(3500m, result1);
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
