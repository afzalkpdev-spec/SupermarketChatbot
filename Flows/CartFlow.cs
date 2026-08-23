using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class CartFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CartService _carts;

    public CartFlow(WhatsAppService whatsApp, ConversationService conversations, CartService carts)
    {
        _whatsApp = whatsApp;
        _conversations = conversations;
        _carts = carts;
    }

    public async Task AddProductAndShowCartAsync(string to, long conversationId, long customerId, int branchId, long productId)
    {
        var cart = await _carts.GetOrCreateActiveCartAsync(customerId, branchId);
        var result = await _carts.AddItemAsync(cart.CartId, productId, branchId);

        switch (result.Outcome)
        {
            case CartService.AddItemOutcome.ProductNotFound:
                await _whatsApp.SendTextAsync(to, "Sorry, that item is no longer available.");
                return;

            case CartService.AddItemOutcome.OutOfStock:
                await _whatsApp.SendTextAsync(to,
                    $"😔 Sorry, *{result.Product?.Name}* is out of stock right now. We couldn't add it to your cart.");
                // Still show the cart as-is, in case they have other items in it.
                await ShowCartSummaryAsync(to, conversationId, cart.CartId);
                return;

            case CartService.AddItemOutcome.Added:
                await ShowCartSummaryAsync(to, conversationId, cart.CartId);
                return;
        }
    }

    public async Task ShowCartSummaryAsync(string to, long conversationId, long cartId)
    {
        var summary = await _carts.GetCartSummaryAsync(cartId);

        if (summary.Lines.Count == 0)
        {
            await _whatsApp.SendTextAsync(to, "Your cart is empty. Tap 🛒 Start Shopping to add items.");
            await _conversations.UpdateStateAsync(conversationId, "main_menu");
            return;
        }

        var lines = summary.Lines.Select(l => $"{l.Quantity}x {l.ProductName} — {summary.Currency} {l.LineTotal:0.00}");
        var body = "🛒 *Your Cart*\n\n" + string.Join("\n", lines) + $"\n\n*Total: {summary.Currency} {summary.Total:0.00}*";

        await _whatsApp.SendButtonsAsync(to, body, new List<WhatsAppButton>
        {
            new() { Id = "CART_CHECKOUT", Title = "✅ Checkout" },
            new() { Id = "MENU_SHOP", Title = "🛍️ Keep Shopping" },
            new() { Id = "CART_REMOVE", Title = "🗑️ Remove Item" },
        });

        await _conversations.UpdateStateAsync(conversationId, "cart_updated",
            new Dictionary<string, object> { ["cart_id"] = cartId });
    }

    public async Task SendRemoveItemListAsync(string to, long conversationId, long cartId)
    {
        var summary = await _carts.GetCartSummaryAsync(cartId);

        if (summary.Lines.Count == 0)
        {
            await _whatsApp.SendTextAsync(to, "Your cart is already empty.");
            return;
        }

        await _whatsApp.SendListAsync(to, "Select an item to remove from your cart:", "Remove Item",
            new List<WhatsAppListSection>
            {
                new()
                {
                    Title = "Cart Items",
                    Rows = summary.Lines.Select(l => new WhatsAppListRow
                    {
                        Id = $"REMOVE_{l.ProductId}",
                        Title = l.ProductName.Length > 24 ? l.ProductName[..24] : l.ProductName,
                        Description = $"Qty {l.Quantity} — {summary.Currency} {l.LineTotal:0.00}"
                    }).ToList()
                }
            });

        await _conversations.UpdateStateAsync(conversationId, "removing_item",
            new Dictionary<string, object> { ["cart_id"] = cartId });
    }

    public async Task HandleRemoveSelectionAsync(string to, long conversationId, long cartId, long productId)
    {
        await _carts.RemoveItemAsync(cartId, productId);
        await ShowCartSummaryAsync(to, conversationId, cartId);
    }
}