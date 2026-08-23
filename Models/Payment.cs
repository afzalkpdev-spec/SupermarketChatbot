using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("payment_id")]
    public long PaymentId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("method")]
    public string Method { get; set; } = "cash_on_delivery";

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "AED";

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}