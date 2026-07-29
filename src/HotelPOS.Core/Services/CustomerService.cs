using HotelPOS.Common.Models;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAppLogger _logger;

    public CustomerService(ICustomerRepository customerRepository, IAppLogger logger)
    {
        _customerRepository = customerRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync(string? searchQuery = null)
    {
        return await _customerRepository.GetCustomersAsync(searchQuery);
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _customerRepository.GetCustomerByIdAsync(id);
    }

    public async Task<Customer?> GetCustomerByPhoneOrIdCardAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;
        return await _customerRepository.GetCustomerByPhoneOrIdCardAsync(identifier);
    }

    public async Task<int> SaveCustomerAsync(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.FullName))
        {
            throw new ArgumentException("ชื่อ-นามสกุลลูกค้าห้ามเป็นค่าว่าง");
        }
        customer.FullName = customer.FullName.Trim();
        return await _customerRepository.SaveCustomerAsync(customer);
    }

    public async Task DeleteCustomerAsync(int id)
    {
        await _customerRepository.DeleteCustomerAsync(id);
    }

    public async Task<IEnumerable<CustomerStayHistoryDto>> GetCustomerStayHistoryAsync(int customerId)
    {
        return await _customerRepository.GetCustomerStayHistoryAsync(customerId);
    }

    public async Task<IEnumerable<CustomerPOSHistoryDto>> GetCustomerPOSHistoryAsync(int customerId)
    {
        return await _customerRepository.GetCustomerPOSHistoryAsync(customerId);
    }
}
