using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Services;

namespace SupermarketBot.Services;

public class AdminOrderService
{
    private readonly AppDbContext _db;
    private readonly OrderStatusService _orderStatus;
    private readonly WhatsAppService _whatsApp;

    public AdminOrderService(AppDbContext db, OrderStatusService orderStatus, WhatsAppService whatsApp)
    {
        _db = db;
        _orderStatus = orderStatus;
        _whatsApp = whatsApp;
    }

    // ---------------------------------------------------------------
    // Read side — for the orders list / detail views in the portal
    // ---------------------------------------------------------------

    public record OrderSummary(
        long OrderId, string OrderNumber, string CustomerName, string CustomerPhone,
        string Status, string FulfillmentType, decimal TotalAmount, string Currency, DateTimeOffset PlacedAt);

    public async Task<List<OrderSummary>> GetOrdersAsync(string? status, int? branchId, int limit = 100)
    {
        var query =
            from o in _db.Orders
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            select new { o, c.FullName, c.WhatsAppNumber };

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.o.Status == status);

        if (branchId.HasValue)
            query = query.Where(x => x.o.BranchId == branchId.Value);

        var results = await query
            .OrderByDescending(x => x.o.PlacedAt)
            .Take(limit)
            .Select(x => new OrderSummary(
                x.o.OrderId, x.o.OrderNumber, x.FullName ?? "Unknown", x.WhatsAppNumber,
                x.o.Status, x.o.FulfillmentType, x.o.TotalAmount, x.o.Currency, x.o.PlacedAt))
            .ToListAsync();

        return results;
    }

    public record OrderItemDetail(string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
    public record OrderDetail(OrderSummary Summary, List<OrderItemDetail> Items, string? AddressLine1, string? DriverName);

    public async Task<OrderDetail?> GetOrderDetailAsync(string orderNumber)
    {
        var orderRow = await (
            from o in _db.Orders
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            where o.OrderNumber == orderNumber
            select new { o, c.FullName, c.WhatsAppNumber }
        ).FirstOrDefaultAsync();

        if (orderRow is null) return null;

        var items = await _db.OrderItems
            .Where(oi => oi.OrderId == orderRow.o.OrderId)
            .Select(oi => new OrderItemDetail(oi.ProductNameSnapshot, oi.Quantity, oi.UnitPrice, oi.LineTotal))
            .ToListAsync();

        string? addressLine1 = null;
        if (orderRow.o.AddressId.HasValue)
        {
            addressLine1 = await _db.Addresses
                .Where(a => a.AddressId == orderRow.o.AddressId.Value)
                .Select(a => a.AddressLine1)
                .FirstOrDefaultAsync();
        }

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == orderRow.o.OrderId);

        var summary = new OrderSummary(
            orderRow.o.OrderId, orderRow.o.OrderNumber, orderRow.FullName ?? "Unknown", orderRow.WhatsAppNumber,
            orderRow.o.Status, orderRow.o.FulfillmentType, orderRow.o.TotalAmount, orderRow.o.Currency, orderRow.o.PlacedAt);

        return new OrderDetail(summary, items, addressLine1, delivery?.DriverName);
    }

    // ---------------------------------------------------------------
    // Write side — status transitions triggered by portal button clicks.
    // Each one performs the DB update via OrderStatusService, then
    // notifies the customer over WhatsApp.
    // ---------------------------------------------------------------

    public async Task<bool> ConfirmOrderAsync(string orderNumber)
    {
        var lookup = await _orderStatus.ConfirmOrderAsync(orderNumber);
        if (lookup is null) return false;

        await _whatsApp.SendTextAsync(lookup.CustomerWhatsAppNumber,
            $"👍 Your order {orderNumber} has been confirmed and will be prepared shortly.");
        return true;
    }

    public async Task<bool> MarkPackedAsync(string orderNumber)
    {
        var lookup = await _orderStatus.MarkPackedAsync(orderNumber);
        if (lookup is null) return false;

        await _whatsApp.SendTextAsync(lookup.CustomerWhatsAppNumber,
            $"📦 Your order {orderNumber} has been packed and is ready to go!");
        return true;
    }

    public async Task<bool> DispatchAsync(string orderNumber, string? driverName, string? driverPhone)
    {
        var lookup = await _orderStatus.DispatchAsync(orderNumber, driverName, driverPhone);
        if (lookup is null) return false;

        var driverText = string.IsNullOrWhiteSpace(driverName) ? "" : $" with {driverName}";
        await _whatsApp.SendTextAsync(lookup.CustomerWhatsAppNumber,
            $"🚚 Your order {orderNumber} is out for delivery{driverText}!");
        return true;
    }

    /// Staff marks it delivered from the portal → customer is asked to confirm receipt.
    /// This mirrors the same customer-facing confirmation step already built for WhatsApp
    /// (RECEIVED_YES_/RECEIVED_NO_ buttons handled in WebhookController).
    public async Task<bool> MarkDeliveredAsync(string orderNumber)
    {
        var lookup = await _orderStatus.MarkDeliveredByDriverAsync(orderNumber);
        if (lookup is null) return false;

        await _whatsApp.SendButtonsAsync(lookup.CustomerWhatsAppNumber,
            $"📬 Your order {orderNumber} was marked as delivered. Did you receive it?",
            new List<WhatsAppButton>
            {
                new() { Id = $"RECEIVED_YES_{lookup.Order.OrderId}", Title = "✅ Yes, got it" },
                new() { Id = $"RECEIVED_NO_{lookup.Order.OrderId}", Title = "❌ Not yet" },
            });
        return true;
    }
}