using SupermarketBot.Services;

namespace SupermarketBot.Flows;

public class GreetingFlow
{
    private readonly WhatsAppService _whatsApp;
    private readonly ConversationService _conversations;
    private readonly CustomerService _customers;
    private readonly OfferService _offers;
    private readonly BotSettingsService _settings;
    private readonly AppConfigService _appConfig;
    private readonly IConfiguration _config;

    public GreetingFlow(
        WhatsAppService whatsApp,
        ConversationService conversations,
        CustomerService customers,
        OfferService offers,
        BotSettingsService settings,
        AppConfigService appConfig,
        IConfiguration config)
    {
        _whatsApp = whatsApp;
        _conversations = conversations;
        _customers = customers;
        _offers = offers;
        _settings = settings;
        _appConfig = appConfig;
        _config = config;
    }

    /// Offer images come from the `offers` table (see OfferService),
    /// filtered to is_active AND within start_date/end_date — managed via
    /// the /admin/offers API (with Cloudinary image upload). No restart
    /// needed to change them.
    ///
    /// Whether offers are shown at all, and whether the shopping menu is
    /// shown at all, are both read from bot_settings (key-value, see
    /// BotSettingsService/BotSettingKeys) — each is looked up by its own
    /// key. Update either any time with, e.g.:
    ///   UPDATE bot_settings SET setting_value = TRUE WHERE setting_key = 'offers_enabled';
    ///   UPDATE bot_settings SET setting_value = TRUE WHERE setting_key = 'shopping_enabled';
    /// Takes effect on the very next message, no restart needed.
    ///
    /// "OfferImages:RepeatIntervalHours" (appsettings.json) is the one
    /// remaining config-based setting here — how often (in hours) a
    /// customer sees offers again. 24 = daily, 168 = weekly, etc.
    ///
    /// The intro text sent right before the images is the CAPTION of the
    /// first offer (lowest display_order) — no separate settings row.
    /// If that offer's caption is null/empty, no intro text is sent, just
    /// the images.
    ///
    /// When shopping is disabled, the bot sends ONLY the intro text +
    /// offer images (if offers are also enabled) and nothing else — no
    /// greeting, no buttons.
    public async Task SendMainMenuAsync(string to, long conversationId, long customerId)
    {
        var offersEnabled = await _settings.GetFlagAsync(BotSettingKeys.OffersEnabled);

        if (offersEnabled)
        {
            var repeatIntervalHours = _config.GetValue<double?>("OfferImages:RepeatIntervalHours") ?? 24;
            var shouldShow = await _customers.ShouldShowOfferImagesAsync(customerId, repeatIntervalHours);

            if (shouldShow)
            {
                var activeOffers = await _offers.GetActiveOffersAsync();
                if (activeOffers.Count > 0)
                {
                    // First offer in display order supplies the intro text via its caption.
                    var introText = activeOffers[0].Caption;
                    if (!string.IsNullOrWhiteSpace(introText))
                    {
                        await _whatsApp.SendTextAsync(to, introText);
                    }

                    await _whatsApp.SendImagesAsync(to, activeOffers.Select(o => o.ImageUrl));
                    await _customers.MarkOfferImagesShownAsync(customerId);
                }
            }
        }

        var shoppingEnabled = await _settings.GetFlagAsync(BotSettingKeys.ShoppingEnabled, defaultValue: true);
        if (!shoppingEnabled)
        {
            // Images-only mode: no menu, no state change — leave the
            // conversation exactly as it was.
            return;
        }

        var isRestaurant = await _appConfig.IsRestaurantAsync();
        var greetingText = isRestaurant
            ? "\ud83d\udc4b Welcome! What would you like to do?"
            : "\ud83d\udc4b Welcome to Fresh Mart! What would you like to do?";
        var shopButtonTitle = isRestaurant ? "\ud83c\udf7d\ufe0f Start Ordering" : "\ud83d\uded2 Start Shopping";

        await _whatsApp.SendButtonsAsync(to, greetingText, new List<WhatsAppButton>
        {
            new() { Id = "MENU_SHOP", Title = shopButtonTitle },
            new() { Id = "MENU_TRACK", Title = "\ud83d\udce6 Track Order" },
            new() { Id = "MENU_SUPPORT", Title = "\ud83d\udcac Support" },
        });

        await _conversations.UpdateStateAsync(conversationId, "main_menu");
    }
}