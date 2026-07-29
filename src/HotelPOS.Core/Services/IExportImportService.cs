using System.Threading.Tasks;

namespace HotelPOS.Core.Services;

public interface IExportImportService
{
    Task ExportCustomersToCsvAsync(string filePath);
    Task<int> ImportCustomersFromCsvAsync(string filePath);
    Task ExportRoomsToCsvAsync(string filePath);
    Task<int> ImportRoomsFromCsvAsync(string filePath);
    Task ExportProductsToCsvAsync(string filePath);
    Task<int> ImportProductsFromCsvAsync(string filePath);
}
