using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class CatalogService
{
    private readonly AppDbContext _db;
    private readonly AppConfigService _appConfig;

    public CatalogService(AppDbContext db, AppConfigService appConfig)
    {
        _db = db;
        _appConfig = appConfig;
    }

    public async Task<List<Category>> GetTopLevelCategoriesAsync()
    {
        var businessType = await _appConfig.GetBusinessTypeAsync();

        return await _db.Categories
            .Where(c => c.ParentId == null)
            .Where(c => c.BusinessType == null || c.BusinessType == businessType)
            .OrderBy(c => c.DisplayOrder)
            .Take(10) // WhatsApp list messages max out at 10 rows per section
            .ToListAsync();
    }

    public record ProductWithStock(long ProductId, string Name, decimal Price, string Currency, int Stock);

    public async Task<List<ProductWithStock>> GetProductsByCategoryAsync(int categoryId, int branchId, int limit = 10)
    {
        // No extra business-type filter needed here — the customer can only
        // ever reach a categoryId that GetTopLevelCategoriesAsync already
        // scoped correctly for them.
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
    /// Scoped to the current BusinessType via the product's category — search
    /// doesn't have an explicit categoryId to rely on like browsing does, so
    /// it needs its own filter to avoid cross-vertical results.
    public async Task<List<ProductWithStock>> SearchProductsByNameAsync(string searchTerm, int branchId, int limit = 10)
    {
        var pattern = $"%{searchTerm.Trim()}%";
        var businessType = await _appConfig.GetBusinessTypeAsync();

        var query =
            from p in _db.Products
            join c in _db.Categories on p.CategoryId equals c.CategoryId into cats
            from c in cats.DefaultIfEmpty()
            where p.IsActive
                  && EF.Functions.ILike(p.Name, pattern)
                  && (c == null || c.BusinessType == null || c.BusinessType == businessType)
            join i in _db.Inventory.Where(i => i.BranchId == branchId)
                on p.ProductId equals i.ProductId into inv
            from i in inv.DefaultIfEmpty()
            orderby p.Name
            select new ProductWithStock(p.ProductId, p.Name, p.Price, p.Currency, i != null ? i.QuantityAvailable : 0);

        return await query.Take(limit).ToListAsync();
    }
}