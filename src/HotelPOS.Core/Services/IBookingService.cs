using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface IBookingService
{
    Task<IEnumerable<Booking>> GetBookingsAsync(DateTime? startDate = null, DateTime? endDate = null, BookingStatus? status = null, int? roomId = null);
    Task<Booking?> GetBookingByIdAsync(int id);
    Task<Booking?> GetBookingByCodeAsync(string bookingCode);
    Task<Booking?> GetActiveBookingByRoomIdAsync(int roomId);
    // Batch: ดึงการจอง active ทั้งหมดพร้อม Customer ใน query เดียว (แก้ N+1)
    Task<Dictionary<int, (Booking Booking, Customer? Customer)>> GetAllActiveBookingsWithCustomersAsync();
    Task<Folio?> GetFolioByBookingIdAsync(int bookingId);

    Task<Booking> WalkInCheckInAsync(int roomId, Customer customer, RatePlanType ratePlan, decimal agreedRate, DateTime plannedCheckOut, string? notes = null, int? createdBy = null);
    Task<Booking> CreateReservationAsync(int roomId, Customer customer, RatePlanType ratePlan, decimal agreedRate, DateTime checkInPlanned, DateTime checkOutPlanned, string? notes = null, int? createdBy = null);
    Task CheckInExistingBookingAsync(int bookingId);
    Task<Folio> CheckOutAsync(int bookingId, decimal extraCharges = 0, decimal discountAmount = 0, string? notes = null);
    Task CancelBookingAsync(int bookingId, string? reason = null);
}
