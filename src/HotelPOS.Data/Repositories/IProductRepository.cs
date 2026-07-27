using System.Collections.Generic;
using System.Threading.Tasks;
using HotelPOS.Common.Models;

namespace HotelPOS.Data.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<ProductCategory>> GetCategoriesAsync();
    Task<ProductCategory?> GetCategoryByIdAsync(int id);
    Task<int> SaveCategoryAsync(ProductCategory category);
    Task DeleteCategoryAsync(int id);

    Task<IEnumerable<Product>> GetProductsAsync(int? categoryId = null, string? searchQuery = null);
    Task<Product?> GetProductByIdAsync(int id);
    Task<int> SaveProductAsync(Product product);
    Task UpdateStockAsync(int productId, int qtyDelta);
    Task DeleteProductAsync(int id);
}
