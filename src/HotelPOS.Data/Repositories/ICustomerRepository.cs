using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface ICustomerRepository
{
    Task<IEnumerable<Customer>> GetCustomersAsync(string? searchQuery = null);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer?> GetCustomerByPhoneOrIdCardAsync(string identifier);
    Task<int> SaveCustomerAsync(Customer customer);
    Task DeleteCustomerAsync(int id);
    Task<IEnumerable<CustomerStayHistoryDto>> GetCustomerStayHistoryAsync(int customerId);
    Task<IEnumerable<CustomerPOSHistoryDto>> GetCustomerPOSHistoryAsync(int customerId);
}
