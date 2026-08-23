using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class BrowsingFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CatalogService _catalog;

    public BrowsingFlow(WhatsAppService whatsApp, ConversationService conversations, CatalogService catalog)
    {
        _whatsApp = whatsApp;
        _conversations = conversations;
        _catalog = catalog;
    }

    public async Task SendCategoryListAsync(string to, long conversationId)
    {
        var categories = await _catalog.GetTopLevelCategoriesAsync();

        if (categories.Count == 0)
        {
            await _whatsApp.SendTextAsync(to, "Sorry, our catalog is being updated. Please check back shortly.");
            return;
        }

        var rows = categories.Select(c => new WhatsAppListRow
        {
            Id = $"CATEGORY_{c.CategoryId}",
            Title = c.Name.Length > 24 ? c.Name[..24] : c.Name
        }).ToList();

        await _whatsApp.SendListAsync(to, "Browse by category:", "View Categories", new List<WhatsAppListSection>
        {
            new() { Title = "Categories", Rows = rows }
        });

        await _conversations.UpdateStateAsync(conversationId, "browsing_category");
    }

    public async Task SendProductListAsync(string to, long conversationId, int categoryId, int branchId)
    {
        var products = await _catalog.GetProductsByCategoryAsync(categoryId, branchId);

        if (products.Count == 0)
        {
            await _whatsApp.SendTextAsync(to, "No products found in this category right now.");
            return;
        }

        var rows = products.Select(p => new WhatsAppListRow
        {
            Id = $"PRODUCT_{p.ProductId}",
            Title = p.Name.Length > 24 ? p.Name[..24] : p.Name,
            Description = $"{p.Currency} {p.Price} — {(p.Stock > 0 ? "In stock" : "Out of stock")}"
        }).ToList();

        await _whatsApp.SendListAsync(to, "Pick a product to add to your cart:", "View Products", new List<WhatsAppListSection>
        {
            new() { Title = "Products", Rows = rows }
        });

        await _conversations.UpdateStateAsync(conversationId, "viewing_product",
            new Dictionary<string, object> { ["selected_category"] = categoryId });
    }

    public async Task SendSearchResultsAsync(string to, long conversationId, string searchTerm, int branchId)
    {
        var products = await _catalog.SearchProductsByNameAsync(searchTerm, branchId);

        if (products.Count == 0)
        {
            await _whatsApp.SendTextAsync(to,
                $"No products found matching \"{searchTerm}\". Try a different name, or tap 🛒 Start Shopping to browse categories.");
            return;
        }

        var rows = products.Select(p => new WhatsAppListRow
        {
            Id = $"PRODUCT_{p.ProductId}",
            Title = p.Name.Length > 24 ? p.Name[..24] : p.Name,
            Description = $"{p.Currency} {p.Price} — {(p.Stock > 0 ? "In stock" : "Out of stock")}"
        }).ToList();

        await _whatsApp.SendListAsync(to, $"Results for \"{searchTerm}\":", "View Results", new List<WhatsAppListSection>
        {
            new() { Title = "Search Results", Rows = rows }
        });

        await _conversations.UpdateStateAsync(conversationId, "viewing_product");
    }
}
