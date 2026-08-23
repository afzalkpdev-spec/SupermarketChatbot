using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class OrderStatusService
{
    private readonly AppDbContext _db;

    public OrderStatusService(AppDbContext db)
    {
        _db = db;
    }

    public record OrderLookup(Order Order, string CustomerWhatsAppNumber);

    private async Task<OrderLookup?> FindOrderWithCustomerAsync(string orderNumber)
    {
        var result = await (
            from o in _db.Orders
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            where o.OrderNumber == orderNumber
            select new { Order = o, c.WhatsAppNumber }
        ).FirstOrDefaultAsync();

        return result is null ? null : new OrderLookup(result.Order, result.WhatsAppNumber);
    }

    public async Task<OrderLookup?> ConfirmOrderAsync(string orderNumber)
    {
        var lookup = await FindOrderWithCustomerAsync(orderNumber);
        if (lookup is null) return null;

        lookup.Order.Status = "confirmed";
        lookup.Order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return lookup;
    }

    public async Task<OrderLookup?> MarkPackedAsync(string orderNumber)
    {
        var lookup = await FindOrderWithCustomerAsync(orderNumber);
        if (lookup is null) return null;

        lookup.Order.Status = "packed";
        lookup.Order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return lookup;
    }

    public async Task<OrderLookup?> DispatchAsync(string orderNumber, string? driverName, string? driverPhone)
    {
        var lookup = await FindOrderWithCustomerAsync(orderNumber);
        if (lookup is null) return null;

        lookup.Order.Status = "out_for_delivery";
        lookup.Order.UpdatedAt = DateTimeOffset.UtcNow;

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == lookup.Order.OrderId);
        if (delivery is null)
        {
            delivery = new Delivery { OrderId = lookup.Order.OrderId };
            _db.Deliveries.Add(delivery);
        }

        delivery.DriverName = driverName;
        delivery.DriverPhone = driverPhone;
        delivery.Status = "en_route";
        delivery.DispatchedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return lookup;
    }

    /// Driver reports delivery — this is NOT final. The order stays
    /// out_for_delivery until the customer confirms receipt (or a
    /// timeout/staff override marks it delivered anyway — not built here).
    public async Task<OrderLookup?> MarkDeliveredByDriverAsync(string orderNumber)
    {
        var lookup = await FindOrderWithCustomerAsync(orderNumber);
        if (lookup is null) return null;

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == lookup.Order.OrderId);
        if (delivery is not null)
        {
            delivery.Status = "delivered"; // driver-reported, pending customer confirmation
            delivery.DeliveredAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return lookup;
    }

    public async Task<Order?> ConfirmDeliveryByCustomerAsync(long orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is null) return null;

        order.Status = "delivered";
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return order;
    }

    /// Customer says they did NOT receive it despite driver marking delivered.
    /// MVP: flags it back to en_route and opens a support ticket for staff follow-up.
    public async Task<Order?> FlagNotReceivedAsync(long orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is null) return null;

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == orderId);
        if (delivery is not null)
        {
            delivery.Status = "en_route"; // revert — dispute needs human follow-up
        }

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO support_tickets (customer_id, order_id, category, description, status)
               VALUES ({order.CustomerId}, {order.OrderId}, 'missing_item',
                       'Customer reported non-delivery despite driver marking delivered.', 'open')");

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetMostRecentOrderAsync(long customerId)
    {
        return await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .FirstOrDefaultAsync();
    }
}