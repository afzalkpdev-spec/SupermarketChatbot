using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class AppConfigService
{
    private readonly AppDbContext _db;

    public AppConfigService(AppDbContext db)
    {
        _db = db;
    }

    /// Reads the single app_config row. Falls back to "grocery" if the
    /// row/table doesn't exist yet (e.g. migration hasn't been run).
    public async Task<string> GetBusinessTypeAsync()
    {
        var row = await _db.AppConfig.FindAsync(1);
        return row?.BusinessType ?? "grocery";
    }

    public async Task<bool> IsRestaurantAsync()
    {
        var businessType = await GetBusinessTypeAsync();
        return string.Equals(businessType, "restaurant", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetBusinessTypeAsync(string businessType)
    {
        var row = await _db.AppConfig.FindAsync(1);
        if (row is null)
        {
            _db.AppConfig.Add(new AppConfig { Id = 1, BusinessType = businessType });
        }
        else
        {
            row.BusinessType = businessType;
        }
        await _db.SaveChangesAsync();
    }
}