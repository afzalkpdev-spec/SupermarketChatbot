using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupermarketBot.Flows;
using SupermarketBot.Services;

namespace SupermarketBot.Controllers;

[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly CustomerService _customers;
    private readonly ConversationService _conversations;
    private readonly GreetingFlow _greetingFlow;
    private readonly BrowsingFlow _browsingFlow;
    private readonly CartFlow _cartFlow;
    private readonly CheckoutFlow _checkoutFlow;
    private readonly OrderTrackingFlow _orderTrackingFlow;
    private readonly RestaurantCheckoutFlow _restaurantCheckoutFlow;
    private readonly BotSettingsService _settings;
    private readonly AppConfigService _appConfig;
    private readonly WhatsAppService _whatsApp;
    private readonly ILogger<WebhookController> _logger;

    private const int DefaultBranchId = 1; // TODO: replace with real branch selection logic later

    public WebhookController(
        IConfiguration config,
        CustomerService customers,
        ConversationService conversations,
        GreetingFlow greetingFlow,
        BrowsingFlow browsingFlow,
        CartFlow cartFlow,
        CheckoutFlow checkoutFlow,
        OrderTrackingFlow orderTrackingFlow,
        RestaurantCheckoutFlow restaurantCheckoutFlow,
        BotSettingsService settings,
        AppConfigService appConfig,
        WhatsAppService whatsApp,
        ILogger<WebhookController> logger)
    {
        _config = config;
        _customers = customers;
        _conversations = conversations;
        _greetingFlow = greetingFlow;
        _browsingFlow = browsingFlow;
        _cartFlow = cartFlow;
        _checkoutFlow = checkoutFlow;
        _orderTrackingFlow = orderTrackingFlow;
        _restaurantCheckoutFlow = restaurantCheckoutFlow;
        _settings = settings;
        _appConfig = appConfig;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    // ---------------------------------------------------------------
    // GET — Meta calls this once, when you register the webhook URL,
    // to confirm you control this endpoint.
    // ---------------------------------------------------------------
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expectedToken = _config["WhatsApp:VerifyToken"];

        if (mode == "subscribe" && token == expectedToken)
        {
            _logger.LogInformation("Webhook verified successfully.");
            return Ok(challenge);
        }

        return StatusCode(403);
    }

    // ---------------------------------------------------------------
    // POST — every inbound WhatsApp event (messages, statuses) arrives here.
    // ---------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement body)
    {
        // NOTE: awaited directly rather than fire-and-forget, because
        // scoped services (like AppDbContext) get disposed as soon as this
        // request ends — a detached background task would crash mid-query.
        // This is fine for an MVP; at higher volume, push the payload onto
        // a queue (e.g. a channel, Hangfire, or a message broker) and
        // return 200 immediately, processing the queue separately.
        await ProcessEventAsync(body);
        return Ok();
    }

    private async Task ProcessEventAsync(JsonElement body)
    {
        try
        {
            var message = ExtractMessage(body, out var from, out var profileName, out var messageType, out var rawJson);
            if (message is null)
            {
                LogStatusEventsIfAny(body); // no inbound message — check if this is a status update instead
                return;
            }

            var (inboundText, selectedId) = ParseContent(message.Value);

            var customer = await _customers.FindOrCreateCustomerAsync(from!, profileName);
            var conversation = await _conversations.GetOrCreateConversationAsync(customer.CustomerId);

            await _conversations.LogMessageAsync(conversation.ConversationId, "inbound", messageType!, inboundText, rawJson);

            await RouteMessageAsync(from!, conversation.ConversationId, conversation.CurrentState, conversation.ContextData,
                customer.CustomerId, inboundText, selectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming webhook event");
        }
    }

    /// WhatsApp sends message-status updates (sent/delivered/read/failed)
    /// as separate webhook events from actual inbound messages. Previously
    /// these were silently ignored — logging "failed" ones with their error
    /// details is essential for diagnosing sends that were accepted by the
    /// API but never actually reached the customer (e.g. bad image format,
    /// oversized media, invalid URL).
    private void LogStatusEventsIfAny(JsonElement body)
    {
        try
        {
            if (!body.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0) return;
            var change = entries[0].GetProperty("changes")[0];
            var value = change.GetProperty("value");

            if (!value.TryGetProperty("statuses", out var statuses) || statuses.GetArrayLength() == 0) return;

            foreach (var status in statuses.EnumerateArray())
            {
                var statusValue = status.TryGetProperty("status", out var s) ? s.GetString() : null;
                var recipientId = status.TryGetProperty("recipient_id", out var r) ? r.GetString() : null;

                if (statusValue == "failed" && status.TryGetProperty("errors", out var errors))
                {
                    foreach (var error in errors.EnumerateArray())
                    {
                        var code = error.TryGetProperty("code", out var c) ? c.GetInt32().ToString() : "?";
                        var title = error.TryGetProperty("title", out var t) ? t.GetString() : "";
                        var details = error.TryGetProperty("error_data", out var ed) && ed.TryGetProperty("details", out var d)
                            ? d.GetString() : "";

                        _logger.LogWarning(
                            "WhatsApp message FAILED for {RecipientId} — code {Code}: {Title}. {Details}",
                            recipientId, code, title, details);
                    }
                }
                else
                {
                    _logger.LogInformation("WhatsApp status update: {Status} for {RecipientId}", statusValue, recipientId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing status webhook event");
        }
    }

    private static JsonElement? ExtractMessage(JsonElement body, out string? from, out string? profileName, out string? messageType, out string rawJson)
    {
        from = null;
        profileName = null;
        messageType = null;
        rawJson = body.ToString();

        if (!body.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0) return null;
        var change = entries[0].GetProperty("changes")[0];
        var value = change.GetProperty("value");

        if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0) return null;
        var message = messages[0];

        from = message.GetProperty("from").GetString();
        messageType = message.GetProperty("type").GetString();

        if (value.TryGetProperty("contacts", out var contacts) && contacts.GetArrayLength() > 0)
        {
            profileName = contacts[0].GetProperty("profile").GetProperty("name").GetString();
        }

        return message;
    }

    private static (string? inboundText, string? selectedId) ParseContent(JsonElement message)
    {
        var type = message.GetProperty("type").GetString();

        if (type == "text")
        {
            return (message.GetProperty("text").GetProperty("body").GetString(), null);
        }

        if (type == "interactive")
        {
            var interactive = message.GetProperty("interactive");
            var interactiveType = interactive.GetProperty("type").GetString();

            if (interactiveType == "button_reply")
            {
                return (null, interactive.GetProperty("button_reply").GetProperty("id").GetString());
            }
            if (interactiveType == "list_reply")
            {
                return (null, interactive.GetProperty("list_reply").GetProperty("id").GetString());
            }
        }

        return (null, null);
    }

    // ---------------------------------------------------------------
    // Router: decides what to do based on conversation state + input.
    // State-dependent free-text capture (e.g. address) is checked FIRST,
    // before the generic "hi/hello" greeting shortcut, so typing during
    // a flow doesn't accidentally reset it.
    // ---------------------------------------------------------------
    private async Task RouteMessageAsync(
        string from, long conversationId, string currentState, string contextDataJson, long customerId,
        string? inboundText, string? selectedId)
    {
        // ---- State-dependent free-text capture ----
        if (currentState == "awaiting_address" && !string.IsNullOrWhiteSpace(inboundText))
        {
            await _checkoutFlow.HandleAddressTextAsync(from, conversationId, customerId, inboundText.Trim());
            return;
        }

        if (currentState == "awaiting_restaurant_address" && !string.IsNullOrWhiteSpace(inboundText))
        {
            await _restaurantCheckoutFlow.HandleAddressTextAsync(from, conversationId, customerId, inboundText.Trim());
            return;
        }

        if (currentState == "awaiting_table_number" && !string.IsNullOrWhiteSpace(inboundText))
        {
            await _restaurantCheckoutFlow.HandleTableNumberTextAsync(from, conversationId, inboundText.Trim());
            return;
        }

        // ---- Greeting shortcut (only when not mid-flow on free text) ----
        if (inboundText is not null &&
            System.Text.RegularExpressions.Regex.IsMatch(inboundText.Trim(), "^(hi|hello|hey|start|menu)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            await _greetingFlow.SendMainMenuAsync(from, conversationId, customerId);
            return;
        }

        // ---- Fixed menu / button / list selections ----
        switch (selectedId)
        {
            case "MENU_SHOP":
                await _browsingFlow.SendCategoryListAsync(from, conversationId);
                return;

            case "MENU_TRACK":
            {
                var orderTrackingEnabled = await _settings.GetFlagAsync(BotSettingKeys.OrderTrackingEnabled);
                if (!orderTrackingEnabled)
                {
                    await _whatsApp.SendTextAsync(from, "Order tracking isn't available yet — check back soon!");
                    return;
                }
                await _orderTrackingFlow.ShowLatestOrderStatusAsync(from, customerId);
                return;
            }

            case "MENU_SUPPORT":
                // TODO: build support ticket flow — insert into support_tickets
                await _whatsApp.SendTextAsync(from, "Support flow is coming soon!");
                return;

            case "CART_CHECKOUT":
                if (await _appConfig.IsRestaurantAsync())
                    await _restaurantCheckoutFlow.AskFulfillmentTypeAsync(from, conversationId);
                else
                    await _checkoutFlow.AskFulfillmentTypeAsync(from, conversationId);
                return;

            case "CART_REMOVE":
            {
                var cartId = ExtractCartId(contextDataJson);
                if (cartId is null)
                {
                    await _whatsApp.SendTextAsync(from, "Couldn't find your cart. Please tap 🛒 Start Shopping to begin again.");
                    return;
                }
                await _cartFlow.SendRemoveItemListAsync(from, conversationId, cartId.Value);
                return;
            }

            case "FULFILL_DELIVERY":
            case "FULFILL_PICKUP":
            case "FULFILL_DINEIN":
            case "FULFILL_TAKEAWAY":
                if (await _appConfig.IsRestaurantAsync())
                    await _restaurantCheckoutFlow.HandleFulfillmentChoiceAsync(from, conversationId, selectedId);
                else
                    await _checkoutFlow.HandleFulfillmentChoiceAsync(from, conversationId, selectedId!);
                return;

            case "TIME_ASAP":
            case "TIME_TODAY":
            case "TIME_TOMORROW":
                // Restaurant flow doesn't currently use time slots (dine-in/takeaway
                // skip straight to payment, delivery is ASAP-only for now) — this
                // case only fires for the grocery flow.
                await _checkoutFlow.HandleTimeSlotChoiceAsync(from, conversationId, selectedId);
                return;

            case "PAY_COD":
                if (await _appConfig.IsRestaurantAsync())
                    await _restaurantCheckoutFlow.FinalizeOrderAsync(from, conversationId, customerId, DefaultBranchId);
                else
                    await _checkoutFlow.FinalizeOrderAsync(from, conversationId, customerId, DefaultBranchId);
                return;
        }

        if (selectedId is not null && selectedId.StartsWith("CATEGORY_"))
        {
            var categoryId = int.Parse(selectedId.Replace("CATEGORY_", ""));
            await _browsingFlow.SendProductListAsync(from, conversationId, categoryId, DefaultBranchId);
            return;
        }

        if (selectedId is not null && selectedId.StartsWith("PRODUCT_"))
        {
            var productId = long.Parse(selectedId.Replace("PRODUCT_", ""));
            await _cartFlow.AddProductAndShowCartAsync(from, conversationId, customerId, DefaultBranchId, productId);
            return;
        }

        if (selectedId is not null && selectedId.StartsWith("REMOVE_"))
        {
            var productId = long.Parse(selectedId.Replace("REMOVE_", ""));
            var cartId = ExtractCartId(contextDataJson);
            if (cartId is null)
            {
                await _whatsApp.SendTextAsync(from, "Couldn't find your cart. Please tap 🛒 Start Shopping to begin again.");
                return;
            }
            await _cartFlow.HandleRemoveSelectionAsync(from, conversationId, cartId.Value, productId);
            return;
        }

        if (selectedId is not null && selectedId.StartsWith("RECEIVED_YES_"))
        {
            var orderId = long.Parse(selectedId.Replace("RECEIVED_YES_", ""));
            await _orderTrackingFlow.HandleDeliveryConfirmedAsync(from, orderId);
            return;
        }

        if (selectedId is not null && selectedId.StartsWith("RECEIVED_NO_"))
        {
            var orderId = long.Parse(selectedId.Replace("RECEIVED_NO_", ""));
            await _orderTrackingFlow.HandleDeliveryNotReceivedAsync(from, orderId);
            return;
        }

        // ---- Free-text that isn't a recognized command: treat as a product search ----
        // This must come after all button/list handling above (selectedId is always
        // null for typed text, so it won't intercept any interactive replies).
        // Gated behind the DB-backed ShoppingEnabled flag too — otherwise a customer
        // could bypass "images only" mode just by typing a product name directly.
        var shoppingEnabledForSearch = await _settings.GetFlagAsync(BotSettingKeys.ShoppingEnabled, defaultValue: true);
        if (shoppingEnabledForSearch && !string.IsNullOrWhiteSpace(inboundText))
        {
            await _browsingFlow.SendSearchResultsAsync(from, conversationId, inboundText.Trim(), DefaultBranchId);
            return;
        }

        // Fallback — didn't match any known state/intent
        await _greetingFlow.SendMainMenuAsync(from, conversationId, customerId);
    }

    private static long? ExtractCartId(string contextDataJson)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(contextDataJson)?.AsObject();
            if (node is not null && node.ContainsKey("cart_id"))
            {
                return node["cart_id"]!.GetValue<long>();
            }
        }
        catch
        {
            // malformed/empty context — treat as no cart found
        }
        return null;
    }
}