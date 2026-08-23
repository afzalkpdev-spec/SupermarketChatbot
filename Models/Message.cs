using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketBot.Models;

[Table("messages")]
public class Message
{
    [Key]
    [Column("message_id")]
    public long MessageId { get; set; }

    [Column("conversation_id")]
    public long ConversationId { get; set; }

    [Column("direction")]
    public string Direction { get; set; } = "inbound"; // inbound, outbound

    [Column("message_type")]
    public string MessageType { get; set; } = "text";

    [Column("content")]
    public string? Content { get; set; }

    [Column("payload", TypeName = "jsonb")]
    public string? Payload { get; set; }

    [Column("sent_at")]
    public DateTimeOffset SentAt { get; set; }
}
