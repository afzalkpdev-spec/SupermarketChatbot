using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("products")]
public class Product
{
    [Key]
    [Column("product_id")]
    public long ProductId { get; set; }

    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("brand")]
    public string? Brand { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "AED";

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
