using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using HotelPOS.Common;
using HotelPOS.Common.Models;
using HotelPOS.Logging;

namespace HotelPOS.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IAppLogger _logger;

    public ProductRepository(DbConnectionFactory connectionFactory, IAppLogger logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductCategory>> GetCategoriesAsync()
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, name AS Name, is_active AS IsActive
                FROM product_categories
                WHERE is_active = 1
                ORDER BY name";
            return await connection.QueryAsync<ProductCategory>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านข้อมูลประเภทสินค้าไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<ProductCategory?> GetCategoryByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, name AS Name, is_active AS IsActive
                FROM product_categories
                WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<ProductCategory>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลประเภทสินค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveCategoryAsync(ProductCategory category)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (category.Id == 0)
            {
                const string sql = @"
                    INSERT INTO product_categories (name, is_active)
                    VALUES (@Name, 1);
                    SELECT last_insert_rowid();";
                var id = await connection.ExecuteScalarAsync<long>(sql, category);
                category.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างประเภทสินค้าใหม่ '{category.Name}' (ID: {category.Id}) สำเร็จ", correlationId);
                return category.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE product_categories
                    SET name = @Name, is_active = @IsActive
                    WHERE id = @Id;";
                await connection.ExecuteAsync(sql, category);
                _logger.Info(LogCategory.Database, $"แก้ไขประเภทสินค้า ID={category.Id} ('{category.Name}') สำเร็จ", correlationId);
                return category.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกประเภทสินค้า '{category.Name}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE product_categories SET is_active = 0 WHERE id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id });
            _logger.Info(LogCategory.Database, $"ลบประเภทสินค้า ID={id} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ลบประเภทสินค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<IEnumerable<Product>> GetProductsAsync(int? categoryId = null, string? searchQuery = null)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, category_id AS CategoryId, name AS Name, sku AS Sku,
                       price AS Price, cost AS Cost, stock_qty AS StockQty,
                       track_stock AS TrackStock, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM products
                WHERE is_active = 1";

            var conditions = new List<string>();
            var param = new DynamicParameters();

            if (categoryId.HasValue)
            {
                conditions.Add("category_id = @CatId");
                param.Add("CatId", categoryId.Value);
            }
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                conditions.Add("(name LIKE @Q OR sku LIKE @Q)");
                param.Add("Q", $"%{searchQuery.Trim()}%");
            }

            if (conditions.Count > 0)
            {
                sql += " AND " + string.Join(" AND ", conditions);
            }

            sql += " ORDER BY name";

            return await connection.QueryAsync<Product>(sql, param);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, "อ่านข้อมูลสินค้าไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT id AS Id, category_id AS CategoryId, name AS Name, sku AS Sku,
                       price AS Price, cost AS Cost, stock_qty AS StockQty,
                       track_stock AS TrackStock, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM products
                WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"อ่านข้อมูลสินค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task<int> SaveProductAsync(Product product)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            if (product.Id == 0)
            {
                const string sql = @"
                    INSERT INTO products (category_id, name, sku, price, cost, stock_qty, track_stock, is_active, created_at, updated_at)
                    VALUES (@CategoryId, @Name, @Sku, @Price, @Cost, @StockQty, @TrackStock, 1, datetime('now','localtime'), datetime('now','localtime'));
                    SELECT last_insert_rowid();";
                var id = await connection.ExecuteScalarAsync<long>(sql, product);
                product.Id = (int)id;
                _logger.Info(LogCategory.Database, $"สร้างข้อมูลสินค้าใหม่ '{product.Name}' (ID: {product.Id}) สำเร็จ", correlationId);
                return product.Id;
            }
            else
            {
                const string sql = @"
                    UPDATE products
                    SET category_id = @CategoryId, name = @Name, sku = @Sku,
                        price = @Price, cost = @Cost, stock_qty = @StockQty,
                        track_stock = @TrackStock, is_active = @IsActive,
                        updated_at = datetime('now','localtime')
                    WHERE id = @Id;";
                await connection.ExecuteAsync(sql, product);
                _logger.Info(LogCategory.Database, $"แก้ไขข้อมูลสินค้า ID={product.Id} ('{product.Name}') สำเร็จ", correlationId);
                return product.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"บันทึกข้อมูลสินค้า '{product.Name}' ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task UpdateStockAsync(int productId, int qtyDelta)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE products
                SET stock_qty = stock_qty + @Delta,
                    updated_at = datetime('now','localtime')
                WHERE id = @Id AND track_stock = 1;";
            await connection.ExecuteAsync(sql, new { Id = productId, Delta = qtyDelta });
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ปรับปรุงสต็อกสินค้า ID={productId} (Delta={qtyDelta}) ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }

    public async Task DeleteProductAsync(int id)
    {
        var correlationId = _logger.NewCorrelationId();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE products SET is_active = 0 WHERE id = @Id;";
            await connection.ExecuteAsync(sql, new { Id = id });
            _logger.Info(LogCategory.Database, $"ลบสินค้า ID={id} สำเร็จ", correlationId);
        }
        catch (Exception ex)
        {
            _logger.Error(LogCategory.Database, $"ลบสินค้า ID={id} ไม่สำเร็จ", ex, correlationId);
            throw;
        }
    }
}
