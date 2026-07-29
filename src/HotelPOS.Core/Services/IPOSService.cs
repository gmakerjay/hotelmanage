using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Common.Models;

namespace HotelPOS.Core.Services;

public interface IPOSService
{
    Task<IEnumerable<ProductCategory>> GetCategoriesAsync();
    Task<ProductCategory?> GetCategoryByIdAsync(int id);
    Task<int> SaveCategoryAsync(ProductCategory category);
    Task DeleteCategoryAsync(int id);

    Task<IEnumerable<Product>> GetProductsAsync(int? categoryId = null, string? query = null);
    Task<Product?> GetProductByIdAsync(int id);
    Task<int> SaveProductAsync(Product product);
    Task DeleteProductAsync(int id);

    Task<int> SubmitSaleAsync(Sale sale, List<SaleItem> items, Payment? payment);
    Task<IEnumerable<Sale>> GetSalesAsync(DateTime start, DateTime end);
    Task<Sale?> GetSaleByIdAsync(int id);
    Task<IEnumerable<SaleItem>> GetSaleItemsBySaleIdAsync(int saleId);
    Task<IEnumerable<Payment>> GetPaymentsBySaleIdAsync(int saleId);

    // Helper to get active checked-in bookings/folios
    Task<IEnumerable<dynamic>> GetActiveFoliosAsync();

    // ดึงข้อมูล Room + Customer จาก Folio ID (สำหรับใบเสร็จ POS)
    Task<(Room Room, Customer Customer)?> GetFolioDetailsAsync(int folioId);

    // ดึงข้อมูลลูกค้าจาก ID (สำหรับใบเสร็จ POS กรณีลูกค้า Walk-in)
    Task<Customer?> GetCustomerByIdAsync(int customerId);

    // ยกเลิกบิลการขาย คืนสต็อก และหัก Folio
    Task VoidSaleAsync(int saleId);
}
