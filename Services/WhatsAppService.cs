using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SupermarketBot.Services;

public class WhatsAppButton
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class WhatsAppListRow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class WhatsAppListSection
{
    public string Title { get; set; } = string.Empty;
    public List<WhatsAppListRow> Rows { get; set; } = new();
}

public class WhatsAppService
{
    private readonly HttpClient _http;
    private readonly string _phoneNumberId;

    public WhatsAppService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        var graphVersion = config["WhatsApp:GraphApiVersion"];
        _phoneNumberId = config["WhatsApp:PhoneNumberId"]!;
        var token = config["WhatsApp:AccessToken"];

        _http = httpClientFactory.CreateClient();
        _http.BaseAddress = new Uri($"https://graph.facebook.com/{graphVersion}/{_phoneNumberId}/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task SendTextAsync(string to, string body)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body }
        };
        await PostAsync(payload);
    }

    public async Task SendButtonsAsync(string to, string bodyText, List<WhatsAppButton> buttons)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new
                {
                    buttons = buttons.Select(b => new
                    {
                        type = "reply",
                        reply = new { id = b.Id, title = b.Title }
                    })
                }
            }
        };
        await PostAsync(payload);
    }

    public async Task SendListAsync(string to, string bodyText, string buttonLabel, List<WhatsAppListSection> sections)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = bodyText },
                action = new
                {
                    button = buttonLabel,
                    sections = sections.Select(s => new
                    {
                        title = s.Title,
                        rows = s.Rows.Select(r => new
                        {
                            id = r.Id,
                            title = r.Title,
                            description = r.Description
                        })
                    })
                }
            }
        };
        await PostAsync(payload);
    }

    /// Sends a single image message. imageUrl must be publicly reachable —
    /// Meta's servers fetch it directly, so localhost/authenticated URLs won't work.
    public async Task SendImageAsync(string to, string imageUrl, string? caption = null)
    {
        object imagePayload = string.IsNullOrWhiteSpace(caption)
            ? new { link = imageUrl }
            : new { link = imageUrl, caption };

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "image",
            image = imagePayload
        };
        await PostAsync(payload);
    }

    /// WhatsApp has no "multiple images in one message" type — each image is
    /// its own API call. Sent sequentially (awaited one at a time) so they
    /// arrive in the chat in the order given, right before whatever message
    /// follows them.
    public async Task SendImagesAsync(string to, IEnumerable<string> imageUrls)
    {
        foreach (var url in imageUrls)
        {
            await SendImageAsync(to, url);
        }
    }

    private async Task PostAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("messages", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            // In production, log this via ILogger instead of throwing raw.
            throw new HttpRequestException(
                $"WhatsApp API call failed: {response.StatusCode} — {errorBody}");
        }
    }
}