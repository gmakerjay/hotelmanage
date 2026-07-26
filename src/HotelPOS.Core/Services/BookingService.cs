using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IFolioRepository _folioRepository;
    private readonly IAppLogger _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IRoomRepository roomRepository,
        ICustomerRepository customerRepository,
        IFolioRepository folioRepository,
        IAppLogger logger)
    {
        _bookingRepository = bookingRepository;
        _roomRepository = roomRepository;
        _customerRepository = customerRepository;
        _folioRepository = folioRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Booking>> GetBookingsAsync(DateTime? startDate = null, DateTime? endDate = null, BookingStatus? status = null, int? roomId = null)
    {
        return await _bookingRepository.GetBookingsAsync(startDate, endDate, status, roomId);
    }

    public async Task<Booking?> GetBookingByIdAsync(int id)
    {
        return await _bookingRepository.GetBookingByIdAsync(id);
    }

    public async Task<Booking?> GetBookingByCodeAsync(string bookingCode)
    {
        return await _bookingRepository.GetBookingByCodeAsync(bookingCode);
    }

    public async Task<Booking?> GetActiveBookingByRoomIdAsync(int roomId)
    {
        return await _bookingRepository.GetActiveBookingByRoomIdAsync(roomId);
    }

    public async Task<Folio?> GetFolioByBookingIdAsync(int bookingId)
    {
        return await _folioRepository.GetFolioByBookingIdAsync(bookingId);
    }

    public async Task<Booking> WalkInCheckInAsync(int roomId, Customer customer, RatePlanType ratePlan, decimal agreedRate, DateTime plannedCheckOut, string? notes = null, int? createdBy = null)
    {
        var correlationId = _logger.NewCorrelationId();
        var room = await _roomRepository.GetRoomByIdAsync(roomId);
        if (room == null) throw new KeyNotFoundException($"ไม่พบห้องพัก ID={roomId}");

        if (room.Status != RoomStatus.Available && room.Status != RoomStatus.Cleaning)
        {
            throw new InvalidOperationException($"ห้องพัก {room.RoomNumber} อยู่ในสถานะ {room.Status} ไม่สามารถเช็คอิน Walk-in ได้");
        }

        // บันทึก/อัปเดตข้อมูลลูกค้า
        if (customer.Id == 0)
        {
            await _customerRepository.SaveCustomerAsync(customer);
        }

        var now = DateTime.Now;
        var bookingCode = $"BK-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        var booking = new Booking
        {
            BookingCode = bookingCode,
            RoomId = roomId,
            CustomerId = customer.Id,
            RatePlan = ratePlan,
            CheckInPlanned = now,
            CheckOutPlanned = plannedCheckOut,
            CheckInActual = now,
            Status = BookingStatus.CheckedIn,
            AgreedRate = agreedRate,
            Notes = notes,
            CreatedBy = createdBy
        };

        await _bookingRepository.SaveBookingAsync(booking);

        // คำนวณค่าห้องพักตั้งต้น
        var initialRoomCharges = CalculateRoomCharges(ratePlan, agreedRate, now, plannedCheckOut);
        var folio = new Folio
        {
            BookingId = booking.Id,
            RoomCharges = initialRoomCharges,
            ExtraCharges = 0,
            DiscountAmount = 0,
            TotalAmount = initialRoomCharges,
            IsClosed = false
        };
        await _folioRepository.SaveFolioAsync(folio);

        // เปลี่ยนสถานะห้องเป็น Occupied
        await _roomRepository.UpdateRoomStatusAsync(roomId, RoomStatus.Occupied, notes);
        _logger.Info(LogCategory.Booking, $"Walk-in เช็คอินห้อง {room.RoomNumber} (BookingCode: {bookingCode}) สำเร็จ", correlationId);

        return booking;
    }

    public async Task<Booking> CreateReservationAsync(int roomId, Customer customer, RatePlanType ratePlan, decimal agreedRate, DateTime checkInPlanned, DateTime checkOutPlanned, string? notes = null, int? createdBy = null)
    {
        var correlationId = _logger.NewCorrelationId();
        var room = await _roomRepository.GetRoomByIdAsync(roomId);
        if (room == null) throw new KeyNotFoundException($"ไม่พบห้องพัก ID={roomId}");

        if (customer.Id == 0)
        {
            await _customerRepository.SaveCustomerAsync(customer);
        }

        var now = DateTime.Now;
        var bookingCode = $"BK-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

        var booking = new Booking
        {
            BookingCode = bookingCode,
            RoomId = roomId,
            CustomerId = customer.Id,
            RatePlan = ratePlan,
            CheckInPlanned = checkInPlanned,
            CheckOutPlanned = checkOutPlanned,
            Status = BookingStatus.Reserved,
            AgreedRate = agreedRate,
            Notes = notes,
            CreatedBy = createdBy
        };

        await _bookingRepository.SaveBookingAsync(booking);

        var initialRoomCharges = CalculateRoomCharges(ratePlan, agreedRate, checkInPlanned, checkOutPlanned);
        var folio = new Folio
        {
            BookingId = booking.Id,
            RoomCharges = initialRoomCharges,
            ExtraCharges = 0,
            DiscountAmount = 0,
            TotalAmount = initialRoomCharges,
            IsClosed = false
        };
        await _folioRepository.SaveFolioAsync(folio);

        if (room.Status == RoomStatus.Available)
        {
            await _roomRepository.UpdateRoomStatusAsync(roomId, RoomStatus.Reserved, notes);
        }

        _logger.Info(LogCategory.Booking, $"สร้างการจองล่วงหน้าห้อง {room.RoomNumber} (BookingCode: {bookingCode}) สำเร็จ", correlationId);
        return booking;
    }

    public async Task CheckInExistingBookingAsync(int bookingId)
    {
        var correlationId = _logger.NewCorrelationId();
        var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
        if (booking == null) throw new KeyNotFoundException($"ไม่พบรายการจอง ID={bookingId}");

        if (booking.Status != BookingStatus.Reserved)
        {
            throw new InvalidOperationException($"การจองไม่ได้อยู่ในสถานะจองไว้ (สถานะปัจจุบัน: {booking.Status})");
        }

        var now = DateTime.Now;
        await _bookingRepository.UpdateBookingStatusAsync(bookingId, BookingStatus.CheckedIn, actualCheckIn: now);
        await _roomRepository.UpdateRoomStatusAsync(booking.RoomId, RoomStatus.Occupied);

        _logger.Info(LogCategory.Booking, $"เช็คอินจากการจองล่วงหน้า Code '{booking.BookingCode}' สำเร็จ", correlationId);
    }

    public async Task<Folio> CheckOutAsync(int bookingId, decimal extraCharges = 0, decimal discountAmount = 0, string? notes = null)
    {
        var correlationId = _logger.NewCorrelationId();
        var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
        if (booking == null) throw new KeyNotFoundException($"ไม่พบรายการจอง ID={bookingId}");

        if (booking.Status != BookingStatus.CheckedIn)
        {
            throw new InvalidOperationException($"การจองต้องอยู่ในสถานะ CheckedIn ก่อนเช็คเอาท์ (สถานะปัจจุบัน: {booking.Status})");
        }

        var now = DateTime.Now;
        var checkInTime = booking.CheckInActual ?? booking.CheckInPlanned;
        var finalRoomCharges = CalculateRoomCharges(booking.RatePlan, booking.AgreedRate, checkInTime, now);
        var totalAmount = Math.Max(0, finalRoomCharges + extraCharges - discountAmount);

        var folio = await _folioRepository.GetFolioByBookingIdAsync(bookingId);
        if (folio == null)
        {
            folio = new Folio
            {
                BookingId = bookingId,
                RoomCharges = finalRoomCharges,
                ExtraCharges = extraCharges,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                IsClosed = true,
                ClosedAt = now
            };
            await _folioRepository.SaveFolioAsync(folio);
        }
        else
        {
            await _folioRepository.CloseFolioAsync(folio.Id, finalRoomCharges, extraCharges, discountAmount, totalAmount);
            folio.RoomCharges = finalRoomCharges;
            folio.ExtraCharges = extraCharges;
            folio.DiscountAmount = discountAmount;
            folio.TotalAmount = totalAmount;
            folio.IsClosed = true;
            folio.ClosedAt = now;
        }

        await _bookingRepository.UpdateBookingStatusAsync(bookingId, BookingStatus.CheckedOut, actualCheckOut: now);
        await _roomRepository.UpdateRoomStatusAsync(booking.RoomId, RoomStatus.Cleaning, "เช็คเอาท์แล้ว - รอทำความสะอาด");

        _logger.Info(LogCategory.Booking, $"เช็คเอาท์ Booking Code '{booking.BookingCode}' รวมยอดบิล = {totalAmount} บาท สำเร็จ", correlationId);
        return folio;
    }

    public async Task CancelBookingAsync(int bookingId, string? reason = null)
    {
        var correlationId = _logger.NewCorrelationId();
        var booking = await _bookingRepository.GetBookingByIdAsync(bookingId);
        if (booking == null) throw new KeyNotFoundException($"ไม่พบรายการจอง ID={bookingId}");

        await _bookingRepository.UpdateBookingStatusAsync(bookingId, BookingStatus.Cancelled);
        var activeBooking = await _bookingRepository.GetActiveBookingByRoomIdAsync(booking.RoomId);
        if (activeBooking == null)
        {
            await _roomRepository.UpdateRoomStatusAsync(booking.RoomId, RoomStatus.Available, reason);
        }

        _logger.Info(LogCategory.Booking, $"ยกเลิกการจอง Code '{booking.BookingCode}' สำเร็จ", correlationId);
    }

    private static decimal CalculateRoomCharges(RatePlanType ratePlan, decimal agreedRate, DateTime start, DateTime end)
    {
        if (end <= start) end = start.AddHours(1);

        switch (ratePlan)
        {
            case RatePlanType.Hourly:
                var hours = (int)Math.Max(1, Math.Ceiling((end - start).TotalHours));
                return hours * agreedRate;

            case RatePlanType.Daily:
                var days = (int)Math.Max(1, Math.Ceiling((end - start).TotalDays));
                return days * agreedRate;

            case RatePlanType.Monthly:
                return agreedRate;

            default:
                return agreedRate;
        }
    }
}
