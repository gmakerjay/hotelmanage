using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Data;
using HotelPOS.Data.Repositories;
using HotelPOS.Logging;

namespace HotelPOS.Core.Services;

public class POSService : IPOSService
{
    private readonly IProductRepository _productRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public POSService(
        IProductRepository productRepository,
        ISaleRepository saleRepository,
        DbConnectionFactory connectionFactory,
        IAppLogger logger)
    {
        _productRepository = productRepository;
        _saleRepository = saleRepository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductCategory>> GetCategoriesAsync()
    {
        return await _productRepository.GetCategoriesAsync();
    }

    public async Task<ProductCategory?> GetCategoryByIdAsync(int id)
    {
        return await _productRepository.GetCategoryByIdAsync(id);
    }

    public async Task<int> SaveCategoryAsync(ProductCategory category)
    {
        return await _productRepository.SaveCategoryAsync(category);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        await _productRepository.DeleteCategoryAsync(id);
    }

    public async Task<IEnumerable<Product>> GetProductsAsync(int? categoryId = null, string? query = null)
    {
        return await _productRepository.GetProductsAsync(categoryId, query);
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _productRepository.GetProductByIdAsync(id);
    }

    public async Task<int> SaveProductAsync(Product product)
    {
        return await _productRepository.SaveProductAsync(product);
    }

    public async Task DeleteProductAsync(int id)
    {
        await _productRepository.DeleteProductAsync(id);
    }

    public async Task<int> SubmitSaleAsync(Sale sale, List<SaleItem> items, Payment? payment)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            // 1. Generate unique sale code if empty
            if (string.IsNullOrWhiteSpace(sale.SaleCode))
            {
                sale.SaleCode = $"SL-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(100, 999)}";
            }

            // 2. Validate products and stock quantity
            foreach (var item in items)
            {
                var prod = await _productRepository.GetProductByIdAsync(item.ProductId);
                if (prod == null)
                {
                    throw new InvalidOperationException($"ไม่พบสินค้า ID={item.ProductId}");
                }

                if (prod.TrackStock && prod.StockQty < item.Quantity)
                {
                    throw new InvalidOperationException($"สินค้า '{prod.Name}' มีสต็อกไม่เพียงพอ (คงเหลือ: {prod.StockQty}, ต้องการ: {item.Quantity})");
                }

                // Snap the snapshot name and unit price
                item.ProductNameSnapshot = prod.Name;
                item.UnitPrice = prod.Price;
                item.LineTotal = prod.Price * item.Quantity;
            }

            // 3. Calculate total values
            decimal subTotal = 0;
            foreach (var item in items)
            {
                subTotal += item.LineTotal;
            }

            sale.SubTotal = subTotal;
            sale.TotalAmount = subTotal - sale.DiscountAmount + sale.TaxAmount;

            if (payment != null)
            {
                payment.Amount = sale.TotalAmount;
            }

            // 4. Save to Repository
            return await _saleRepository.CreateSaleAsync(sale, items, payment);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Pos, $"สร้างบิลการขายล้มเหลว", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<Sale>> GetSalesAsync(DateTime start, DateTime end)
    {
        return await _saleRepository.GetSalesAsync(start, end);
    }

    public async Task<Sale?> GetSaleByIdAsync(int id)
    {
        return await _saleRepository.GetSaleByIdAsync(id);
    }

    public async Task<IEnumerable<SaleItem>> GetSaleItemsBySaleIdAsync(int saleId)
    {
        return await _saleRepository.GetSaleItemsBySaleIdAsync(saleId);
    }

    public async Task<IEnumerable<Payment>> GetPaymentsBySaleIdAsync(int saleId)
    {
        return await _saleRepository.GetPaymentsBySaleIdAsync(saleId);
    }

    public async Task<IEnumerable<dynamic>> GetActiveFoliosAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT f.id AS FolioId, r.room_number AS RoomNumber, c.full_name AS GuestName, b.id AS BookingId
                FROM folios f
                JOIN bookings b ON f.booking_id = b.id
                JOIN rooms r ON b.room_id = r.id
                JOIN customers c ON b.customer_id = c.id
                WHERE f.is_closed = 0 AND b.status = 1
                ORDER BY r.room_number";
            return await connection.QueryAsync<dynamic>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านข้อมูล Folio ที่ยังไม่ปิดไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<(Room Room, Customer Customer)?> GetFolioDetailsAsync(int folioId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT r.id AS RoomId, r.room_number, r.room_type_id, r.floor, r.status AS RoomStatus, r.notes AS RoomNotes,
                       c.id AS CustomerId, c.full_name, c.phone, c.email
                FROM folios f
                JOIN bookings b ON f.booking_id = b.id
                JOIN rooms r ON b.room_id = r.id
                JOIN customers c ON b.customer_id = c.id
                WHERE f.id = @FolioId";
            var data = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { FolioId = folioId });
            if (data == null) return null;

            var room = new Room
            {
                Id = (int)data.RoomId,
                RoomNumber = data.room_number,
                RoomTypeId = (int)data.room_type_id,
                Floor = data.floor,
                Status = (RoomStatus)data.RoomStatus,
                Notes = data.RoomNotes
            };
            var customer = new Customer
            {
                Id = (int)data.CustomerId,
                FullName = data.full_name,
                Phone = data.phone,
                Email = data.email
            };
            return (room, customer);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ดึงข้อมูล Folio ID={folioId} ไม่สำเร็จ", ex, correlationId);
            return null;
        }
    }

    public async Task<Customer?> GetCustomerByIdAsync(int customerId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Customer>(
                "SELECT * FROM customers WHERE id = @Id", new { Id = customerId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ดึงข้อมูลลูกค้า ID={customerId} ไม่สำเร็จ", ex, correlationId);
            return null;
        }
    }
}
