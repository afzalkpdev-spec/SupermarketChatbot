using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class OrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Order> CreateOrderFromCartAsync(
        long customerId,
        long cartId,
        int branchId,
        string fulfillmentType,
        long? addressId,
        decimal deliveryFee,
        string? tableNumber = null)
    {
        var cartItems = await (
            from ci in _db.CartItems
            join p in _db.Products on ci.ProductId equals p.ProductId
            where ci.CartId == cartId
            select new { ci, p }
        ).ToListAsync();

        if (cartItems.Count == 0)
            throw new InvalidOperationException("Cannot create an order from an empty cart.");

        var subtotal = cartItems.Sum(x => x.ci.Quantity * x.ci.UnitPrice);
        var total = subtotal + deliveryFee;

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            CustomerId = customerId,
            BranchId = branchId,
            AddressId = addressId,
            FulfillmentType = fulfillmentType,
            TableNumber = tableNumber,
            Subtotal = subtotal,
            DiscountAmount = 0,
            DeliveryFee = deliveryFee,
            TotalAmount = total,
            Currency = "AED",
            Status = "placed",
            PlacedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(); // need OrderId before inserting order_items

        foreach (var item in cartItems)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = item.p.ProductId,
                ProductNameSnapshot = item.p.Name,
                Quantity = item.ci.Quantity,
                UnitPrice = item.ci.UnitPrice,
                LineTotal = item.ci.Quantity * item.ci.UnitPrice
            });
        }

        await _db.SaveChangesAsync();
        return order;
    }

    private static string GenerateOrderNumber()
    {
        // e.g. ORD-20260805-4821
        var datePart = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var randomPart = Random.Shared.Next(1000, 9999);
        return $"ORD-{datePart}-{randomPart}";
    }
}