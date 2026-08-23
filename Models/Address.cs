using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("addresses")]
public class Address
{
    [Key]
    [Column("address_id")]
    public long AddressId { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("label")]
    public string? Label { get; set; }

    [Column("address_line1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Column("address_line2")]
    public string? AddressLine2 { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}