using System.Text.Json;
using System.Text.Json.Nodes;
using SupermarketBot.Data;
using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class CheckoutFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CartService _carts;
    private readonly AddressService _addresses;
    private readonly OrderService _orders;
    private readonly PaymentService _payments;
    private readonly AppDbContext _db;

    private const decimal DeliveryFee = 10.00m;

    public CheckoutFlow(
        WhatsAppService whatsApp,
        ConversationService conversations,
        CartService carts,
        AddressService addresses,
        OrderService orders,
        PaymentService payments,
        AppDbContext db)
    {
        _whatsApp = whatsApp;
        _conversations = conversations;
        _carts = carts;
        _addresses = addresses;
        _orders = orders;
        _payments = payments;
        _db = db;
    }

    // ---------------------------------------------------------------
    // Step 1: Delivery or Pickup
    // ---------------------------------------------------------------
    public async Task AskFulfillmentTypeAsync(string to, long conversationId)
    {
        await _whatsApp.SendButtonsAsync(to, "How would you like to receive your order?", new List<WhatsAppButton>
        {
            new() { Id = "FULFILL_DELIVERY", Title = "🚚 Delivery" },
            new() { Id = "FULFILL_PICKUP", Title = "🏬 Pickup" },
        });

        await _conversations.UpdateStateAsync(conversationId, "awaiting_fulfillment_choice");
    }

    public async Task HandleFulfillmentChoiceAsync(string to, long conversationId, string selectedId)
    {
        var fulfillmentType = selectedId == "FULFILL_DELIVERY" ? "delivery" : "pickup";

        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["fulfillment_type"] = fulfillmentType });

        if (fulfillmentType == "delivery")
        {
            await _whatsApp.SendTextAsync(to, "Please type your delivery address (building, street, area):");
            await _conversations.UpdateStateAsync(conversationId, "awaiting_address");
        }
        else
        {
            await AskTimeSlotAsync(to, conversationId);
        }
    }

    // ---------------------------------------------------------------
    // Step 2 (delivery only): capture free-text address
    // ---------------------------------------------------------------
    public async Task HandleAddressTextAsync(string to, long conversationId, long customerId, string addressText)
    {
        var address = await _addresses.SaveAddressAsync(customerId, addressText);

        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["address_id"] = address.AddressId });

        await AskTimeSlotAsync(to, conversationId);
    }

    // ---------------------------------------------------------------
    // Step 3: Time slot
    // ---------------------------------------------------------------
    public async Task AskTimeSlotAsync(string to, long conversationId)
    {
        await _whatsApp.SendButtonsAsync(to, "When would you like it?", new List<WhatsAppButton>
        {
            new() { Id = "TIME_ASAP", Title = "⚡ ASAP" },
            new() { Id = "TIME_TODAY", Title = "🕓 Today, 4-6 PM" },
            new() { Id = "TIME_TOMORROW", Title = "📅 Tomorrow AM" },
        });

        await _conversations.UpdateStateAsync(conversationId, "awaiting_time_slot");
    }

    public async Task HandleTimeSlotChoiceAsync(string to, long conversationId, string selectedId)
    {
        var slotLabel = selectedId switch
        {
            "TIME_ASAP" => "ASAP",
            "TIME_TODAY" => "Today, 4-6 PM",
            "TIME_TOMORROW" => "Tomorrow AM",
            _ => "ASAP"
        };

        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["time_slot"] = slotLabel });

        await AskPaymentMethodAsync(to, conversationId);
    }

    // ---------------------------------------------------------------
    // Step 4: Payment method
    // ---------------------------------------------------------------
    public async Task AskPaymentMethodAsync(string to, long conversationId)
    {
        await _whatsApp.SendButtonsAsync(to, "How would you like to pay?", new List<WhatsAppButton>
        {
            new() { Id = "PAY_COD", Title = "💵 Cash on Delivery" },
            // TODO: add PAY_CARD / PAY_WHATSAPP once a payment gateway is wired up
        });

        await _conversations.UpdateStateAsync(conversationId, "awaiting_payment_method");
    }

    // ---------------------------------------------------------------
    // Step 5: Finalize — create order, record payment, clear cart
    // ---------------------------------------------------------------
    public async Task FinalizeOrderAsync(string to, long conversationId, long customerId, int branchId)
    {
        var context = await GetContextAsync(conversationId);

        var cartId = context["cart_id"]?.GetValue<long>();
        var fulfillmentType = context["fulfillment_type"]?.GetValue<string>() ?? "delivery";
        var addressId = context.ContainsKey("address_id") ? context["address_id"]?.GetValue<long>() : null;
        var deliveryFee = fulfillmentType == "delivery" ? DeliveryFee : 0m;

        if (cartId is null)
        {
            await _whatsApp.SendTextAsync(to, "Something went wrong finding your cart. Please start again by typing 'hi'.");
            return;
        }

        var order = await _orders.CreateOrderFromCartAsync(
            customerId, cartId.Value, branchId, fulfillmentType, addressId, deliveryFee);

        await _payments.RecordCashOnDeliveryAsync(order.OrderId, order.TotalAmount, order.Currency);
        await _carts.ClearCartAsync(cartId.Value);

        var confirmation =
            $"✅ *Order Confirmed!*\n\n" +
            $"Order #: {order.OrderNumber}\n" +
            $"Type: {(fulfillmentType == "delivery" ? "Delivery 🚚" : "Pickup 🏬")}\n" +
            $"Total: {order.Currency} {order.TotalAmount:0.00}\n" +
            $"Payment: Cash on Delivery\n\n" +
            $"We'll notify you as your order progresses. Thank you for shopping with Fresh Mart!";

        await _whatsApp.SendTextAsync(to, confirmation);
        await _conversations.UpdateStateAsync(conversationId, "idle", resetContext: true);
    }

    // ---------------------------------------------------------------
    private async Task<JsonObject> GetContextAsync(long conversationId)
    {
        var conversation = await _db.Conversations.FindAsync(conversationId);
        var json = conversation?.ContextData ?? "{}";
        return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
    }
}