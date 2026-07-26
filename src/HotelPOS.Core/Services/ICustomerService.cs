using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetCustomersAsync(string? searchQuery = null);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer?> GetCustomerByPhoneOrIdCardAsync(string identifier);
    Task<int> SaveCustomerAsync(Customer customer);
    Task DeleteCustomerAsync(int id);
}
