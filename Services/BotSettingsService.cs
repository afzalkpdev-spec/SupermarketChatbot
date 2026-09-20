using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

/// Well-known setting keys — defined as constants so call sites use
/// these instead of typing raw strings, which avoids the "typo in a
/// string literal" risk that key-value tables are normally prone to.
public static class BotSettingKeys
{
    public const string ShoppingEnabled = "shopping_enabled";
    public const string OffersEnabled = "offers_enabled";
    public const string OrderTrackingEnabled = "order_tracking_enabled";
}

public class BotSettingsService
{
    private readonly AppDbContext _db;

    public BotSettingsService(AppDbContext db)
    {
        _db = db;
    }

    /// Reads a single row by key. Returns `defaultValue` if the key
    /// doesn't exist yet (e.g. a new flag added in code before its
    /// row was inserted) — never throws for a missing key.
    public async Task<bool> GetFlagAsync(string key, bool defaultValue = false)
    {
        var row = await _db.BotSettings.FindAsync(key);
        return row?.SettingValue ?? defaultValue;
    }

    public async Task SetFlagAsync(string key, bool value)
    {
        var row = await _db.BotSettings.FindAsync(key);
        if (row is null)
        {
            _db.BotSettings.Add(new BotSetting { SettingKey = key, SettingValue = value });
        }
        else
        {
            row.SettingValue = value;
        }
        await _db.SaveChangesAsync();
    }
}