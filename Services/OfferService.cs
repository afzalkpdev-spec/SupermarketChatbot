using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;

namespace SupermarketBot.Services;

public class OfferService
{
    private readonly AppDbContext _db;

    public OfferService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<string>> GetActiveImageUrlsAsync()
    {
        var now = DateTimeOffset.UtcNow;

        return await _db.Offers
            .Where(o => o.IsActive)
            .Where(o => o.StartDate == null || o.StartDate <= now)
            .Where(o => o.EndDate == null || o.EndDate >= now)
            .OrderBy(o => o.DisplayOrder)
            .Select(o => o.ImageUrl)
            .ToListAsync();
    }
}