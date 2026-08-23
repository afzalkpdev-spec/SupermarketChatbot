using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class CatalogService
{
    private readonly AppDbContext _db;

    public CatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Category>> GetTopLevelCategoriesAsync()
    {
        return await _db.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.DisplayOrder)
            .Take(10) // WhatsApp list messages max out at 10 rows per section
            .ToListAsync();
    }

    public record ProductWithStock(long ProductId, string Name, decimal Price, string Currency, int Stock);

    public async Task<List<ProductWithStock>> GetProductsByCategoryAsync(int categoryId, int branchId, int limit = 10)
    {
        var query =
            from p in _db.Products
            where p.CategoryId == categoryId && p.IsActive
            join i in _db.Inventory.Where(i => i.BranchId == branchId)
                on p.ProductId equals i.ProductId into inv
            from i in inv.DefaultIfEmpty()
            orderby p.Name
            select new ProductWithStock(p.ProductId, p.Name, p.Price, p.Currency, i != null ? i.QuantityAvailable : 0);

        return await query.Take(limit).ToListAsync();
    }

    /// Case-insensitive partial match on product name, e.g. "milk" matches "Full Fat Milk 1L".
    public async Task<List<ProductWithStock>> SearchProductsByNameAsync(string searchTerm, int branchId, int limit = 10)
    {
        var pattern = $"%{searchTerm.Trim()}%";

        var query =
            from p in _db.Products
            where p.IsActive && EF.Functions.ILike(p.Name, pattern)
            join i in _db.Inventory.Where(i => i.BranchId == branchId)
                on p.ProductId equals i.ProductId into inv
            from i in inv.DefaultIfEmpty()
            orderby p.Name
            select new ProductWithStock(p.ProductId, p.Name, p.Price, p.Currency, i != null ? i.QuantityAvailable : 0);

        return await query.Take(limit).ToListAsync();
    }
}
