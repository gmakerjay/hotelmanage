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
