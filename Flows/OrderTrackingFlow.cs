using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class OrderTrackingFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly OrderStatusService _orderStatus;

    public OrderTrackingFlow(WhatsAppService whatsApp, OrderStatusService orderStatus)
    {
        _whatsApp = whatsApp;
        _orderStatus = orderStatus;
    }

    public async Task ShowLatestOrderStatusAsync(string to, long customerId)
    {
        var order = await _orderStatus.GetMostRecentOrderAsync(customerId);

        if (order is null)
        {
            await _whatsApp.SendTextAsync(to, "You don't have any orders yet. Tap 🛒 Start Shopping to place one!");
            return;
        }

        var statusLabel = order.Status switch
        {
            "placed" => "📝 Order placed — awaiting confirmation",
            "confirmed" => "✅ Confirmed — being prepared",
            "packed" => "📦 Packed — ready for dispatch",
            "out_for_delivery" => "🚚 Out for delivery",
            "delivered" => "✅ Delivered",
            "picked_up" => "✅ Picked up",
            "cancelled" => "❌ Cancelled",
            "refunded" => "💰 Refunded",
            _ => order.Status
        };

        var body =
            $"*Order {order.OrderNumber}*\n" +
            $"Status: {statusLabel}\n" +
            $"Total: {order.Currency} {order.TotalAmount:0.00}\n" +
            $"Placed: {order.PlacedAt:dd MMM, h:mm tt}";

        await _whatsApp.SendTextAsync(to, body);
    }

    public async Task HandleDeliveryConfirmedAsync(string to, long orderId)
    {
        var order = await _orderStatus.ConfirmDeliveryByCustomerAsync(orderId);
        if (order is null)
        {
            await _whatsApp.SendTextAsync(to, "Couldn't find that order.");
            return;
        }

        await _whatsApp.SendTextAsync(to, $"🎉 Great! Order {order.OrderNumber} is now marked as delivered. Thanks for shopping with us!");
    }

    public async Task HandleDeliveryNotReceivedAsync(string to, long orderId)
    {
        var order = await _orderStatus.FlagNotReceivedAsync(orderId);
        if (order is null)
        {
            await _whatsApp.SendTextAsync(to, "Couldn't find that order.");
            return;
        }

        await _whatsApp.SendTextAsync(to,
            $"We're sorry about that — we've flagged order {order.OrderNumber} for our team to follow up with you shortly.");
    }
}