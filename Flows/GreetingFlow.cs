using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class GreetingFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CustomerService _customers;
    private readonly IConfiguration _config;
    private readonly OfferService _offers;

    public GreetingFlow(WhatsAppService whatsApp, ConversationService conversations, CustomerService customers, OfferService offers, IConfiguration config)
    {
        _whatsApp = whatsApp;
        _conversations = conversations;
        _customers = customers;
        _offers = offers;
        _config = config;
    }

    /// Offer images are sent before the greeting if the customer hasn't
    /// been shown them within the configured repeat interval — see
    /// "OfferImages:RepeatIntervalHours" in appsettings.json. Set to 24 for
    /// daily, 168 for weekly, or any custom value. Set "OfferImages:Enabled"
    /// to false to turn this off entirely without removing the config.
    public async Task SendMainMenuAsync(string to, long conversationId, long customerId)
    {
        var offersEnabled = _config.GetValue<bool?>("OfferImages:Enabled") ?? true;

        if (offersEnabled)
        {
            var repeatIntervalHours = _config.GetValue<double?>("OfferImages:RepeatIntervalHours") ?? 24;
            var shouldShow = await _customers.ShouldShowOfferImagesAsync(customerId, repeatIntervalHours);
            //var imageUrls = await _offers.GetActiveImageUrlsAsync();
           
            if (shouldShow)
            {
                var imageUrls = await _offers.GetActiveImageUrlsAsync();
                if (imageUrls.Count > 0)
                {
                    await _whatsApp.SendImagesAsync(to, imageUrls);
                    await _customers.MarkOfferImagesShownAsync(customerId);
                }
            }
        }

        await _whatsApp.SendButtonsAsync(to, "\ud83d\udc4b Welcome to Fresh Mart! What would you like to do?", new List<WhatsAppButton>
        {
            new() { Id = "MENU_SHOP", Title = "\ud83d\uded2 Start Shopping" },
            new() { Id = "MENU_TRACK", Title = "\ud83d\udce6 Track Order" },
            new() { Id = "MENU_SUPPORT", Title = "\ud83d\udcac Support" },
        });

        await _conversations.UpdateStateAsync(conversationId, "main_menu");
    }
}