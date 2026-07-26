using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IFolioRepository
{
    Task<Folio?> GetFolioByBookingIdAsync(int bookingId);
    Task<Folio?> GetFolioByIdAsync(int id);
    Task<int> SaveFolioAsync(Folio folio);
    Task CloseFolioAsync(int folioId, decimal roomCharges, decimal extraCharges, decimal discountAmount, decimal totalAmount);
}
