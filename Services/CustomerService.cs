using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class CustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Customer> FindOrCreateCustomerAsync(string whatsAppNumber, string? fullName)
    {
        var existing = await _db.Customers
            .FirstOrDefaultAsync(c => c.WhatsAppNumber == whatsAppNumber);

        if (existing is not null)
        {
            existing.LastActiveAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var customer = new Customer
        {
            WhatsAppNumber = whatsAppNumber,
            FullName = fullName,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        // Create loyalty account via raw SQL since we don't have a mapped
        // entity for it in this starter — extend Models/ if you want full EF tracking.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO loyalty_accounts (customer_id) VALUES ({customer.CustomerId})");

        return customer;
    }

    /// True if this customer has never been shown the offer images, or it's
    /// been at least `repeatIntervalHours` since they last were. A brand-new
    /// customer (LastOfferShownAt is null) is always eligible.
    public async Task<bool> ShouldShowOfferImagesAsync(long customerId, double repeatIntervalHours)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return false;

        if (customer.LastOfferShownAt is null) return true;

        var hoursSinceShown = (DateTimeOffset.UtcNow - customer.LastOfferShownAt.Value).TotalHours;
        return hoursSinceShown >= repeatIntervalHours;
    }

    public async Task MarkOfferImagesShownAsync(long customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return;

        customer.LastOfferShownAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}