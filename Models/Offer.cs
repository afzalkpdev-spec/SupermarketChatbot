using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("offers")]
public class Offer
{
    [Key]
    [Column("offer_id")]
    public long OfferId { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("start_date")]
    public DateTimeOffset? StartDate { get; set; }

    [Column("end_date")]
    public DateTimeOffset? EndDate { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}