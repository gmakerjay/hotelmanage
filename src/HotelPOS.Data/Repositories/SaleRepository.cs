using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public SaleRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<int> CreateSaleAsync(Sale sale, IEnumerable<SaleItem> items, Payment? payment)
    {
        var correlationId = _logger.NewCorrelationId();
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Insert Sale record
            const string saleSql = @"
                INSERT INTO sales (sale_code, folio_id, customer_id, sub_total, discount_amount, tax_amount, total_amount, created_by, created_at, is_deleted)
                VALUES (@SaleCode, @FolioId, @CustomerId, @SubTotal, @DiscountAmount, @TaxAmount, @TotalAmount, @CreatedBy, datetime('now','localtime'), 0);
                SELECT last_insert_rowid();";

            var saleId = await connection.ExecuteScalarAsync<long>(saleSql, sale, transaction);
            sale.Id = (int)saleId;

            // 2. Insert items
            foreach (var item in items)
            {
                item.SaleId = sale.Id;
                const string itemSql = @"
                    INSERT INTO sale_items (sale_id, product_id, product_name_snapshot, unit_price, quantity, line_total)
                    VALUES (@SaleId, @ProductId, @ProductNameSnapshot, @UnitPrice, @Quantity, @LineTotal);";
                await connection.ExecuteAsync(itemSql, item, transaction);

                // Update stock if tracked
                const string stockSql = @"
                    UPDATE products
                    SET stock_qty = stock_qty - @Qty
                    WHERE id = @ProdId AND track_stock = 1;";
                await connection.ExecuteAsync(stockSql, new { Qty = item.Quantity, ProdId = item.ProductId }, transaction);
            }

            // 3. Save payment if exists
            if (payment != null)
            {
                payment.SaleId = sale.Id;
                const string paymentSql = @"
                    INSERT INTO payments (sale_id, method, amount, reference_no, paid_at, received_by)
                    VALUES (@SaleId, @Method, @Amount, @ReferenceNo, datetime('now','localtime'), @ReceivedBy);";
                await connection.ExecuteAsync(paymentSql, new {
                    payment.SaleId,
                    Method = (int)payment.Method,
                    payment.Amount,
                    payment.ReferenceNo,
                    payment.ReceivedBy
                }, transaction);
            }

            // 4. Update Folio if charged to room
            if (sale.FolioId.HasValue)
            {
                const string folioSql = @"
                    UPDATE folios
                    SET extra_charges = extra_charges + @Amount,
                        total_amount = total_amount + @Amount
                    WHERE id = @FolioId;";
                await connection.ExecuteAsync(folioSql, new { Amount = sale.TotalAmount, FolioId = sale.FolioId.Value }, transaction);
            }

            transaction.Commit();
            _logger.Info(LogCategory.Database, $"บันทึกการขายสำเร็จ บิล: {sale.SaleCode} (ID: {sale.Id}) ยอดรวม: {sale.TotalAmount}", correlationId);
            return sale.Id;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.Error(LogCategory.Database, $"บันทึกการขาย บิล: {sale.SaleCode} ล้มเหลว", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<Sale>> GetSalesAsync(DateTime startDate, DateTime endDate)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, sale_code AS SaleCode, folio_id AS FolioId, customer_id AS CustomerId,
                       sub_total AS SubTotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                       total_amount AS TotalAmount, created_by AS CreatedBy, created_at AS CreatedAt, is_deleted AS IsDeleted
                FROM sales
                WHERE date(created_at) BETWEEN date(@Start) AND date(@End) AND is_deleted = 0
                ORDER BY id DESC";
            return await connection.QueryAsync<Sale>(sql, new {
                Start = startDate.ToString("yyyy-MM-dd"),
                End = endDate.ToString("yyyy-MM-dd")
            });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านข้อมูลรายการขายไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Sale?> GetSaleByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, sale_code AS SaleCode, folio_id AS FolioId, customer_id AS CustomerId,
                       sub_total AS SubTotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                       total_amount AS TotalAmount, created_by AS CreatedBy, created_at AS CreatedAt, is_deleted AS IsDeleted
                FROM sales
                WHERE id = @Id AND is_deleted = 0";
            return await connection.QuerySingleOrDefaultAsync<Sale>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลรายการขาย ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<SaleItem>> GetSaleItemsBySaleIdAsync(int saleId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, sale_id AS SaleId, product_id AS ProductId,
                       product_name_snapshot AS ProductNameSnapshot, unit_price AS UnitPrice,
                       quantity AS Quantity, line_total AS LineTotal
                FROM sale_items
                WHERE sale_id = @SaleId";
            return await connection.QueryAsync<SaleItem>(sql, new { SaleId = saleId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลรายการย่อยการขาย SaleID={saleId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<Payment>> GetPaymentsBySaleIdAsync(int saleId)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, sale_id AS SaleId, method AS Method, amount AS Amount,
                       reference_no AS ReferenceNo, paid_at AS PaidAt, received_by AS ReceivedBy
                FROM payments
                WHERE sale_id = @SaleId";
            return await connection.QueryAsync<Payment>(sql, new { SaleId = saleId });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลรายการชำระเงิน SaleID={saleId} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
