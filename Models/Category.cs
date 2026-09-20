using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("categories")]
public class Category
{
    [Key]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("icon_url")]
    public string? IconUrl { get; set; }

     [Column("business_type")]
    public string? BusinessType { get; set; } // "grocery", "restaurant", or null = shown for any vertical
}
