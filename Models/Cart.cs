using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("carts")]
public class Cart
{
    [Key]
    [Column("cart_id")]
    public long CartId { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("branch_id")]
    public int? BranchId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active"; // active, abandoned, converted

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}