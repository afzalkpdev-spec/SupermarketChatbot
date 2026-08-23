using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("deliveries")]
public class Delivery
{
    [Key]
    [Column("delivery_id")]
    public long DeliveryId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("driver_name")]
    public string? DriverName { get; set; }

    [Column("driver_phone")]
    public string? DriverPhone { get; set; }

    [Column("provider")]
    public string? Provider { get; set; }

    [Column("tracking_url")]
    public string? TrackingUrl { get; set; }

    [Column("status")]
    public string Status { get; set; } = "assigned";

    [Column("dispatched_at")]
    public DateTimeOffset? DispatchedAt { get; set; }

    [Column("delivered_at")]
    public DateTimeOffset? DeliveredAt { get; set; }
}