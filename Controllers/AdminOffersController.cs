using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;
using SupermarketBot.Services;

namespace SupermarketBot.Controllers;

[ApiController]
[Authorize]
[Route("admin/offers")]
public class AdminOffersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CloudinaryService _cloudinary;

    public AdminOffersController(AppDbContext db, CloudinaryService cloudinary)
    {
        _db = db;
        _cloudinary = cloudinary;
    }

    [HttpGet]
    public async Task<IActionResult> GetOffers()
    {
        var offers = await _db.Offers
            .OrderBy(o => o.DisplayOrder)
            .ThenByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(offers);
    }

    [HttpPost("upload-image")]
    [RequestSizeLimit(10_000_000)] // 10 MB cap
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided." });
        }

        await using var stream = file.OpenReadStream();
        var imageUrl = await _cloudinary.UploadImageAsync(stream, file.FileName, file.ContentType);

        return Ok(new { imageUrl });
    }

    public record CreateOfferRequest(
        string ImageUrl, string? Caption, bool IsActive,
        DateTimeOffset? StartDate, DateTimeOffset? EndDate, int DisplayOrder);

    [HttpPost]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest request)
    {
        var offer = new Offer
        {
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Offers.Add(offer);
        await _db.SaveChangesAsync();

        return Ok(offer);
    }

    public record UpdateOfferRequest(
        string? Caption, bool? IsActive,
        DateTimeOffset? StartDate, DateTimeOffset? EndDate, int? DisplayOrder);

    [HttpPut("{offerId}")]
    public async Task<IActionResult> UpdateOffer(long offerId, [FromBody] UpdateOfferRequest request)
    {
        var offer = await _db.Offers.FindAsync(offerId);
        if (offer is null) return NotFound(new { message = $"Offer {offerId} not found." });

        if (request.Caption is not null) offer.Caption = request.Caption;
        if (request.IsActive.HasValue) offer.IsActive = request.IsActive.Value;
        if (request.StartDate.HasValue) offer.StartDate = request.StartDate;
        if (request.EndDate.HasValue) offer.EndDate = request.EndDate;
        if (request.DisplayOrder.HasValue) offer.DisplayOrder = request.DisplayOrder.Value;

        await _db.SaveChangesAsync();
        return Ok(offer);
    }

    [HttpDelete("{offerId}")]
    public async Task<IActionResult> DeleteOffer(long offerId)
    {
        var offer = await _db.Offers.FindAsync(offerId);
        if (offer is null) return NotFound(new { message = $"Offer {offerId} not found." });

        _db.Offers.Remove(offer);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Offer deleted." });
    }
}