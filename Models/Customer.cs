using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("customers")]
public class Customer
{
    [Key]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("whatsapp_number")]
    public string WhatsAppNumber { get; set; } = string.Empty;

    [Column("full_name")]
    public string? FullName { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("opted_in_marketing")]
    public bool OptedInMarketing { get; set; }

    [Column("preferred_language")]
    public string PreferredLanguage { get; set; } = "en";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("last_active_at")]
    public DateTimeOffset? LastActiveAt { get; set; }

    [Column("last_offer_shown_at")]
    public DateTimeOffset? LastOfferShownAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active";
}
