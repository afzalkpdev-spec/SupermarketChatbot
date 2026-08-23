using System.Net.Http.Headers;
using System.Text.Json;

namespace SupermarketBot.Services;

public class CloudinaryService
{
    private readonly HttpClient _http;
    private readonly string _cloudName;
    private readonly string _uploadPreset;

    public CloudinaryService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _http = httpClientFactory.CreateClient();
        _cloudName = config["Cloudinary:CloudName"]
            ?? throw new InvalidOperationException("Cloudinary:CloudName is not configured.");
        _uploadPreset = config["Cloudinary:UploadPreset"]
            ?? throw new InvalidOperationException("Cloudinary:UploadPreset is not configured.");
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string? contentType)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(fileStream);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(_uploadPreset), "upload_preset");

        var response = await _http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload", content);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Cloudinary upload failed: {response.StatusCode} — {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("secure_url").GetString()
            ?? throw new InvalidOperationException("Cloudinary response did not include secure_url.");
    }
}