using HotelPOS.Common;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IBookingRepository
{
    Task<IEnumerable<Booking>> GetBookingsAsync(DateTime? startDate = null, DateTime? endDate = null, BookingStatus? status = null, int? roomId = null);
    Task<Booking?> GetBookingByIdAsync(int id);
    Task<Booking?> GetBookingByCodeAsync(string bookingCode);
    Task<Booking?> GetActiveBookingByRoomIdAsync(int roomId);
    // Batch: ดึงการจองที่ active ทั้งหมดพร้อม Customer ใน query เดียว (แก้ N+1)
    Task<Dictionary<int, (Booking Booking, Customer? Customer)>> GetAllActiveBookingsWithCustomersAsync();
    Task<int> SaveBookingAsync(Booking booking);
    Task UpdateBookingStatusAsync(int bookingId, BookingStatus status, DateTime? actualCheckIn = null, DateTime? actualCheckOut = null);
}
