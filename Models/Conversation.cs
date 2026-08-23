using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;

namespace SupermarketBot.Models;

[Table("conversations")]
public class Conversation
{
    [Key]
    [Column("conversation_id")]
    public long ConversationId { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("current_state")]
    public string CurrentState { get; set; } = "idle";

    // Stored as jsonb in Postgres — mapped as a raw string here and
    // (de)serialized manually with System.Text.Json where needed.
    [Column("context_data", TypeName = "jsonb")]
    public string ContextData { get; set; } = "{}";

    [Column("session_started_at")]
    public DateTimeOffset SessionStartedAt { get; set; }

    [Column("last_message_at")]
    public DateTimeOffset LastMessageAt { get; set; }

    [Column("within_24h_window")]
    public bool Within24HWindow { get; set; } = true;
}
