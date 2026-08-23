using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("order_item_id")]
    public long OrderItemId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("product_id")]
    public long ProductId { get; set; }

    [Column("product_name_snapshot")]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("line_total")]
    public decimal LineTotal { get; set; }
}