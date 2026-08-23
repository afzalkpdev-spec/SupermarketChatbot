using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class AddressService
{
    private readonly AppDbContext _db;

    public AddressService(AppDbContext db)
    {
        _db = db;
    }

    /// MVP: saves whatever the customer typed as a single free-text address line.
    /// Extend later with structured fields, geocoding, or saved-address selection.
    public async Task<Address> SaveAddressAsync(long customerId, string freeTextAddress)
    {
        var address = new Address
        {
            CustomerId = customerId,
            AddressLine1 = freeTextAddress,
            IsDefault = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Addresses.Add(address);
        await _db.SaveChangesAsync();
        return address;
    }
}