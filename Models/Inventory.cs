using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("inventory")]
public class Inventory
{
    [Key]
    [Column("inventory_id")]
    public long InventoryId { get; set; }

    [Column("product_id")]
    public long ProductId { get; set; }

    [Column("branch_id")]
    public int BranchId { get; set; }

    [Column("quantity_available")]
    public int QuantityAvailable { get; set; }

    [Column("reorder_threshold")]
    public int ReorderThreshold { get; set; } = 10;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
