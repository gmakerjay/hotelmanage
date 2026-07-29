using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface ISaleRepository
{
    Task<int> CreateSaleAsync(Sale sale, IEnumerable<SaleItem> items, Payment? payment);
    Task<IEnumerable<Sale>> GetSalesAsync(DateTime startDate, DateTime endDate);
    Task<Sale?> GetSaleByIdAsync(int id);
    Task<IEnumerable<SaleItem>> GetSaleItemsBySaleIdAsync(int saleId);
    Task<IEnumerable<Payment>> GetPaymentsBySaleIdAsync(int saleId);
    Task VoidSaleAsync(int saleId);
}
