using System.Text.Json;
using System.Text.Json.Nodes;
using SupermarketBot.Data;
using SupermarketBot.Services;

namespace SupermarketBot.Flows;

/// Restaurant-vertical equivalent of CheckoutFlow. Kept as a separate
/// class (not a modification of CheckoutFlow) so the grocery flow this
/// project started as remains completely untouched and stable. Switch
/// between them via appsettings.json "BusinessType": "grocery" | "restaurant".
public class RestaurantCheckoutFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CartService _carts;
    private readonly AddressService _addresses;
    private readonly OrderService _orders;
    private readonly PaymentService _payments;
    private readonly AppDbContext _db;

    private const decimal DeliveryFee = 10.00m;

    public RestaurantCheckoutFlow(
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
    // Step 1: Dine-in, Takeaway, or Delivery
    // ---------------------------------------------------------------
    public async Task AskFulfillmentTypeAsync(string to, long conversationId)
    {
        await _whatsApp.SendButtonsAsync(to, "How would you like your order?", new List<WhatsAppButton>
        {
            new() { Id = "FULFILL_DINEIN", Title = "🍽️ Dine-in" },
            new() { Id = "FULFILL_TAKEAWAY", Title = "🥡 Takeaway" },
            new() { Id = "FULFILL_DELIVERY", Title = "🚚 Delivery" },
        });

        await _conversations.UpdateStateAsync(conversationId, "awaiting_restaurant_fulfillment_choice");
    }

    public async Task HandleFulfillmentChoiceAsync(string to, long conversationId, string selectedId)
    {
        var fulfillmentType = selectedId switch
        {
            "FULFILL_DINEIN" => "dine_in",
            "FULFILL_TAKEAWAY" => "takeaway",
            _ => "delivery"
        };

        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["fulfillment_type"] = fulfillmentType });

        switch (fulfillmentType)
        {
            case "dine_in":
                await _whatsApp.SendTextAsync(to, "What's your table number?");
                await _conversations.UpdateStateAsync(conversationId, "awaiting_table_number");
                break;

            case "delivery":
                await _whatsApp.SendTextAsync(to, "Please type your delivery address (building, street, area):");
                await _conversations.UpdateStateAsync(conversationId, "awaiting_restaurant_address");
                break;

            default: // takeaway — no address/table needed, straight to payment
                await AskPaymentMethodAsync(to, conversationId);
                break;
        }
    }

    // ---------------------------------------------------------------
    // Step 2a (dine-in only): capture table number
    // ---------------------------------------------------------------
    public async Task HandleTableNumberTextAsync(string to, long conversationId, string tableNumberText)
    {
        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["table_number"] = tableNumberText.Trim() });

        await AskPaymentMethodAsync(to, conversationId);
    }

    // ---------------------------------------------------------------
    // Step 2b (delivery only): capture free-text address
    // ---------------------------------------------------------------
    public async Task HandleAddressTextAsync(string to, long conversationId, long customerId, string addressText)
    {
        var address = await _addresses.SaveAddressAsync(customerId, addressText);

        await _conversations.UpdateStateAsync(conversationId, "pending",
            new Dictionary<string, object> { ["address_id"] = address.AddressId });

        await AskPaymentMethodAsync(to, conversationId);
    }

    // ---------------------------------------------------------------
    // Step 3: Payment method
    // ---------------------------------------------------------------
    public async Task AskPaymentMethodAsync(string to, long conversationId)
    {
        await _whatsApp.SendButtonsAsync(to, "How would you like to pay?", new List<WhatsAppButton>
        {
            new() { Id = "PAY_COD", Title = "💵 Cash" },
            // TODO: add PAY_CARD / PAY_WHATSAPP once a payment gateway is wired up
        });

        await _conversations.UpdateStateAsync(conversationId, "awaiting_payment_method");
    }

    // ---------------------------------------------------------------
    // Step 4: Finalize — create order, record payment, clear cart
    // ---------------------------------------------------------------
    public async Task FinalizeOrderAsync(string to, long conversationId, long customerId, int branchId)
    {
        var context = await GetContextAsync(conversationId);

        var cartId = context["cart_id"]?.GetValue<long>();
        var fulfillmentType = context["fulfillment_type"]?.GetValue<string>() ?? "takeaway";
        var addressId = context.ContainsKey("address_id") ? context["address_id"]?.GetValue<long>() : null;
        var tableNumber = context.ContainsKey("table_number") ? context["table_number"]?.GetValue<string>() : null;
        var deliveryFee = fulfillmentType == "delivery" ? DeliveryFee : 0m;

        if (cartId is null)
        {
            await _whatsApp.SendTextAsync(to, "Something went wrong finding your order. Please start again by typing 'hi'.");
            return;
        }

        var order = await _orders.CreateOrderFromCartAsync(
            customerId, cartId.Value, branchId, fulfillmentType, addressId, deliveryFee, tableNumber);

        await _payments.RecordCashOnDeliveryAsync(order.OrderId, order.TotalAmount, order.Currency);
        await _carts.ClearCartAsync(cartId.Value);

        var typeLabel = fulfillmentType switch
        {
            "dine_in" => $"Dine-in 🍽️ (Table {tableNumber})",
            "takeaway" => "Takeaway 🥡",
            _ => "Delivery 🚚"
        };

        var confirmation =
            $"✅ *Order Confirmed!*\n\n" +
            $"Order #: {order.OrderNumber}\n" +
            $"Type: {typeLabel}\n" +
            $"Total: {order.Currency} {order.TotalAmount:0.00}\n" +
            $"Payment: Cash\n\n" +
            $"We'll notify you as your order progresses. Thank you!";

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