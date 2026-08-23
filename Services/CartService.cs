using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class CartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Cart> GetOrCreateActiveCartAsync(long customerId, int branchId)
    {
        var existing = await _db.Carts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == "active");

        if (existing is not null) return existing;

        var cart = new Cart
        {
            CustomerId = customerId,
            BranchId = branchId,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();
        return cart;
    }

    public enum AddItemOutcome { Added, ProductNotFound, OutOfStock }
    public record AddItemResult(AddItemOutcome Outcome, Product? Product);

    /// Adds a product to the cart, or increments quantity if it's already in there.
    /// Refuses if the product has no stock at the given branch.
    public async Task<AddItemResult> AddItemAsync(long cartId, long productId, int branchId, int quantity = 1)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return new AddItemResult(AddItemOutcome.ProductNotFound, null);

        var inventory = await _db.Inventory
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.BranchId == branchId);

        var availableStock = inventory?.QuantityAvailable ?? 0;

        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId);

        var alreadyInCartQty = existingItem?.Quantity ?? 0;

        // Block if there's no stock at all, or adding more would exceed what's available.
        if (availableStock <= 0 || alreadyInCartQty + quantity > availableStock)
        {
            return new AddItemResult(AddItemOutcome.OutOfStock, product);
        }

        if (existingItem is not null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                CartId = cartId,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price, // price captured at add-time, per schema design
                AddedAt = DateTimeOffset.UtcNow
            });
        }

        var cart = await _db.Carts.FindAsync(cartId);
        if (cart is not null) cart.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return new AddItemResult(AddItemOutcome.Added, product);
    }

    public async Task RemoveItemAsync(long cartId, long productId)
    {
        var item = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId);

        if (item is null) return;

        _db.CartItems.Remove(item);

        var cart = await _db.Carts.FindAsync(cartId);
        if (cart is not null) cart.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    public record CartLine(long ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
    public record CartSummary(long CartId, List<CartLine> Lines, decimal Total, string Currency);

    public async Task<CartSummary> GetCartSummaryAsync(long cartId)
    {
        var lines = await (
            from ci in _db.CartItems
            join p in _db.Products on ci.ProductId equals p.ProductId
            where ci.CartId == cartId
            select new CartLine(p.ProductId, p.Name, ci.Quantity, ci.UnitPrice, ci.Quantity * ci.UnitPrice)
        ).ToListAsync();

        var currency = "AED"; // could be pulled per-product if you support multi-currency
        var total = lines.Sum(l => l.LineTotal);

        return new CartSummary(cartId, lines, total, currency);
    }

    public async Task ClearCartAsync(long cartId)
    {
        var items = _db.CartItems.Where(ci => ci.CartId == cartId);
        _db.CartItems.RemoveRange(items);

        var cart = await _db.Carts.FindAsync(cartId);
        if (cart is not null)
        {
            cart.Status = "converted";
            cart.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}