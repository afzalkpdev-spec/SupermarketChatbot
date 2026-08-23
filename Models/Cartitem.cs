using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("cart_items")]
public class CartItem
{
    [Key]
    [Column("cart_item_id")]
    public long CartItemId { get; set; }

    [Column("cart_id")]
    public long CartId { get; set; }

    [Column("product_id")]
    public long ProductId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("added_at")]
    public DateTimeOffset AddedAt { get; set; }
}