using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("orders")]
public class Order
{
    [Key]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("branch_id")]
    public int BranchId { get; set; }

    [Column("address_id")]
    public long? AddressId { get; set; }

    [Column("fulfillment_type")]
    public string FulfillmentType { get; set; } = "delivery"; // delivery, pickup

    [Column("subtotal")]
    public decimal Subtotal { get; set; }

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [Column("delivery_fee")]
    public decimal DeliveryFee { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "AED";

    [Column("status")]
    public string Status { get; set; } = "placed";

    [Column("placed_at")]
    public DateTimeOffset PlacedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}